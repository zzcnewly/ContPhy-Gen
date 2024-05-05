
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;
using Unity.VisualScripting;
using UnityEngine.Assertions.Must;
using System.ComponentModel;
using UnityEngine.Pool;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using TMPro;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.PlayerLoop;
using System.Runtime.InteropServices;
using UnityEditorInternal;
using UnityEngine.WSA;
using UnityEngine.Diagnostics;
using UnityEditor.Search;
using UnityEngine.Perception.GroundTruth;
using UnityEditor.VersionControl;
using UnityEngine.UIElements;
using Palmmedia.ReportGenerator.Core.Reporting.Builders;
using SoftBodyConfig;
using SamplingFunctions;
using System.Diagnostics;
using System.Globalization;
using UnityEditor.Rendering;
using GeometryIn2D;
using UnityEngine.SocialPlatforms.GameCenter;
using System.Net.Security;
using System.Xml.Serialization;
using PlacementConfig;
using System.Data;
using System.Diagnostics.Tracing;
using UnityEditor.PackageManager.UI;
using UnityEngine.Rendering;

public class WallSampling
{
    public class Stick {
        public Point start, end;
        public int color=-1;
        public virtual string name{
            get{
                return SoftColor.color_name_dict[color] + " Stick";
            }
        }
        public (double, double) center {
            get{
                return ((start.x + end.x) / 2, (start.y + end.y) / 2);
            }
        }
        public (double, double) minxy {
            get{
                return (Math.Min(start.x, end.x), Math.Min(start.y, end.y));
            }
        }
        public (double, double) maxxy {
            get{
                return (Math.Max(start.x, end.x), Math.Max(start.y, end.y));
            }
        }
        public double length{
            get{
                return Math.Sqrt(Math.Pow(start.x - end.x, 2) + Math.Pow(start.y - end.y, 2));
            }
        }
        public double angle{
            get{
                if (end.x - start.x < 0.0001) {return Math.PI / 2;}
                return Math.Atan((end.y - start.y) / (end.x - start.x));
            }
        }

        public void move(double dx, double dy){
            start.x += dx;
            start.y += dy;
            end.x += dx;
            end.y += dy;
        }
        // recpt
        public Vector3 Rotation(){
            float Deg = Math.Abs(start.x - end.x) < 1e-5 ? (start.y > end.y ? 90 : -90) : (float)Math.Atan((float)((start.y - end.y) / (start.x - end.x))) / (float)Math.PI * 180;
            return new Vector3(0, 0, Deg);
        }
        public Vector3 Position(){
            var ct = center;
            float x = (float)center.Item1;
            float y = (float)center.Item2;
            return new Vector3(x, y, 0);
        }
        public virtual Vector3 Scale(){
            return new Vector3((float)length, 1, 1);
        }
        public Dictionary<string, object> GetAll(){
            return new Dictionary<string, object>(){
                {"name", name},
                {"color", color},
                {"position", Position().ToString()},
                {"rotation", Rotation().ToString()},
                {"scale", Scale().ToString()},
                {"startP", (start.x, start.y)},
                {"endP", (end.x, end.y)},
                {"length", length},
                {"angleRad", angle},
            };
        }
    }
    public class Wall : Stick {
        public override string name {
            get{
                return SoftColor.color_name_dict[color] + " Wall";
            }
        }
        public override Vector3 Scale(){
            return new Vector3((float)length, cfg.wall_thickness_onY, cfg.wall_thickness_onZ);
        }
    }
    public class FloatingWall:Wall {
        public override string name {
            get{
                return SoftColor.color_name_dict[color] + " Floating Wall";
            }
        }
    }
    public class Ramp : Wall {
        public bool isLeft = false;
        public override string name {
            get{
                return (isLeft? "Left":"Right") + " Ramp";
            }
        }
    }
    public class FloorPiece : Stick {
        public int seq_from_left = -1;
        public override string name {
            get{
                return "Floor Piece " + SequenceNumberName.Num2Seq[seq_from_left+1];
            }
        }
        public override Vector3 Scale(){
            return new Vector3((float)length, cfg.floor_thickness_onY, cfg.floor_thickness_onZ);
        }
    }
    System.Random rand = new System.Random();

    public WallSampling(int seed) {
        rand = new System.Random(seed);
    }

    static public bool SegmentSegmentDistanceWithinMinMax(Wall stick1, Wall stick2, double D_min, double D_max) {
        double distance = LineSegmentDistance.LineSegmentDist(stick1.start, stick1.end, stick2.start, stick2.end);
        return D_min <= distance && distance <= D_max;
    }

}



public class SoftBall{
    public string name {
        get{
            return SoftColor.color_name_dict[color] + " Ball";
        }
    }
    public int color;
    public string elasticityType;  // "Elastic" or "Rigid" or "Plastic"
    public Vector3 release_pos;
    public Dictionary<string, object> GetAll(){
        return new Dictionary<string, object>(){
            {"name", name},
            {"color", color},
            {"elasticityType", elasticityType},
            {"releasePos", BaseManager.Vec2List(release_pos)},
        };
    }
}


public class SoftBodySampler : Sampler {
    // NOTE: 
    // 1. only Container is in world xyz, 
    //      others are in local xyz, 
    //      the point is the left bottom corner 
    //      of container
    public List<WallSampling.Ramp> ramps;
    public List<WallSampling.FloorPiece> floorPieces;
    public List<SoftBall> balls;
    public List<WallSampling.FloatingWall> floatingWalls;
    public List<float> holes;
    public SoftBodySampler(int seed) : base(seed) { 
        //pass
    }
    
    public void _Sample_Stadium(){
        ramps = new();
        floorPieces = new();
        balls = new();
        floatingWalls = new();
        holes = new();
        var width = (float)Sample_Conrange(cfg.container_size_range);
        var width_ramp = (float)Sample_Conrange(cfg.ramp_w_size_range);
        // rampes
        List<int> clr = new List<int>();
        int clr_id = 0;
        clr.AddRange(cfg.stick_clr_range);
        clr = clr.OrderBy(x => random.Next()).ToList();
        ramps.Add(new WallSampling.Ramp{
            start = new Point{ x = width, y = cfg.y_ramp_start},
            end = new Point{ x =  width + width_ramp, y = cfg.y_ramp_start + cfg.y_ramp_size},
            color = clr[clr_id++],
            isLeft = false,
        });
        ramps.Add(new WallSampling.Ramp{
            start = new Point{ x = -width, y = cfg.y_ramp_start},
            end = new Point{ x =  -width - width_ramp, y = cfg.y_ramp_start + cfg.y_ramp_size},
            color = clr[clr_id++],
            isLeft = true,
        });
        // holes and floor pieces
        var hf_w = cfg.hole_width / 2;
        var range = new List<double>{cfg.rest_width + hf_w, width - hf_w - cfg.rest_width_edge_leave};
        holes.Add((float)Sample_Conrange(range));
        holes.Add(-(float)Sample_Conrange(range));
        floorPieces.Add (
            new WallSampling.FloorPiece{
                start = new Point{ x = holes[0] - hf_w, y = cfg.y_floor_piece},
                end = new Point{ x = holes[1] + hf_w, y = cfg.y_floor_piece},
                color = cfg.color_wall,
                seq_from_left = 1,
            }
        );
        floorPieces.Add (
            new WallSampling.FloorPiece{
                start = new Point{ x = holes[0] + hf_w, y = cfg.y_floor_piece},
                end = new Point{ x = width + cfg.rest_width_floor_piece, y = cfg.y_floor_piece},
                color = cfg.color_wall,
                seq_from_left = 2,
            }
        );
        floorPieces.Add (
            new WallSampling.FloorPiece{
                start = new Point{ x = holes[1] - hf_w, y = cfg.y_floor_piece},
                end = new Point{ x = -width - cfg.rest_width_floor_piece, y = cfg.y_floor_piece},
                color = cfg.color_wall,
                seq_from_left = 0,
            }
        );
        // floating walls
        var wall_point_range = new List<double>() {cfg.rest_width + (-width),width - cfg.rest_width}; 
        float upper1 = 0, lower1 = 0, lower2 = 0, upper2 = 0;
        while (true) {
            upper1 = (float)Sample_Conrange(wall_point_range);
            lower1 = (float)Sample_Conrange(wall_point_range);
            while (Mathf.Abs(lower1 - upper1) < cfg.min_wall_x_length || Mathf.Abs(lower1 - upper1) > cfg.max_wall_x_length){
                lower1 = (float)Sample_Conrange(wall_point_range);
            }
            int temp_times = 0;
            lower2 = (float)Sample_Conrange(wall_point_range);
            while (((lower1 - lower2) * (upper1 - lower2)) < 0 || Mathf.Abs(lower2 - upper1) < cfg.min_wall_x_distance || Mathf.Abs(lower2 - lower1) < cfg.min_wall_x_distance){
                lower2 = (float)Sample_Conrange(wall_point_range);
                temp_times += 1;
                if (temp_times > 200) break;
            }
            if (temp_times > 200) continue;
            else {
                upper2 = lower2;
                break;
            }
        }
        floatingWalls.Add(
            new WallSampling.FloatingWall{
                start = new Point{ x = upper1, y = cfg.y_ramp_start + cfg.y_ramp_size},
                end = new Point{ x = lower1, y = cfg.y_lower_wall_point},
                color = clr[clr_id++],
            }
        );
        floatingWalls.Add(
            new WallSampling.FloatingWall{
                start = new Point{ x = upper2, y = cfg.y_ramp_start + cfg.y_ramp_size},
                end = new Point{ x = lower2, y = cfg.y_lower_wall_point},
                color = clr[clr_id++],
            }
        );
        // balls
        List<int> clr_2 = new List<int>();
        int clr_id_2 = 0;
        clr_2.AddRange(cfg.stick_clr_range);
        clr_2 = clr_2.OrderBy(x => random.Next()).ToList();
        List<string> elasticityType = new List<string>();
        elasticityType.AddRange(cfg.elasticityType);
        elasticityType = elasticityType.OrderBy(x => random.Next()).ToList();
        int ball_id = 0;
        float ball_middle_x = (float)floatingWalls[0].center.Item1; 
        var ball_y_range = new List<double>(){cfg.y_ball_min, cfg.y_ball_max};
        float ball_middle_y = (float)Sample_Conrange(ball_y_range); 
        balls.Add(
            new SoftBall{
                color = clr_2[clr_id_2++],
                elasticityType = elasticityType[ball_id++],
                release_pos = new Vector3(ball_middle_x, ball_middle_y, 0),
            }
        );
        // right ball above the right ramp
        float ball_right_x = (float)ramps[0].center.Item1;
        float ball_right_y = (float)Sample_Conrange(ball_y_range); 
        balls.Add(
            new SoftBall{
                color = clr_2[clr_id_2++],
                elasticityType = elasticityType[ball_id++],
                release_pos = new Vector3(ball_right_x, ball_right_y, 0),
            }
        );
        // left ball above the left ramp
        float ball_left_x = (float)ramps[1].center.Item1;
        float ball_left_y = (float)Sample_Conrange(ball_y_range);
        balls.Add(
            new SoftBall{
                color = clr_2[clr_id_2++],
                elasticityType = elasticityType[ball_id++],
                release_pos = new Vector3(ball_left_x, ball_left_y, 0),
            }
        );
    }

    public void ResampleScene(){
        // clear the cache and resample
        _Sample_Stadium();
    }

    public Dictionary<string, object> GetAll(){
        return new Dictionary<string, object>(){
            {"ramps", ramps.Select(r => r.GetAll()).ToList()},
            {"floorPieces", floorPieces.Select(f => f.GetAll()).ToList()},
            {"balls", balls.Select(b => b.GetAll()).ToList()},
            {"floatingWalls", floatingWalls.Select(f => f.GetAll()).ToList()},
            {"holesCenterXValue", holes},
        };        
    }

}