
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
using FluidSlidesConfig;
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

public class StickSampling
{
    public class Stick {
        public Point start, end;
        public bool isRotatable=false;
        public int color=-1;
        public string name="Stick";
        public string temperature=cfg.NORMAL;
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
        public double end_rot_angle{
            get{
                return (isRotatable? -1 * angle : angle);
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
            float slope = (float)((start.y - end.y) / (start.x - end.x));
            float Deg = (float)Math.Atan(slope) / (float)Math.PI * 180;
            return new Vector3(0, 0, Deg);
        }
        public Vector3 Position(){
            var ct = center;
            float x = (float)center.Item1;
            float y = (float)center.Item2;
            return new Vector3(x, y, 0);
        }
        public Vector3 Scale(){
            return new Vector3((float)length, cfg.receptor_scale_y, cfg.receptor_scale_z);
        }
        public Dictionary<string, object> GetAll(){
            return new Dictionary<string, object>(){
                {"name", name},
                {"color", color},
                {"temperature", temperature},
                {"isRotatable", isRotatable},
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

    System.Random rand = new System.Random();

    public StickSampling(int seed) {
        rand = new System.Random(seed);
    }

    static public List<List<Point>> rotatorTriangles(Stick Rotator){
        double x1 = Rotator.center.Item1;
        double y1 = Rotator.center.Item2;
        double r1 = Rotator.length / 2;
        double slope = Math.Tan(Rotator.angle);
        double y2 = slope * r1 + y1;
        double x2 = x1 + r1;
        double y3 = -1 * slope * r1 + y1;
        double x3 = x2;
        double x2_ = x1 - r1;
        double y2_ = y2;
        double x3_ = x2_;
        double y3_ = y3;
        Point p1 = new Point{x = x1, y = y1};
        Point p2 = new Point{x = x2, y = y2};
        Point p3 = new Point{x = x3, y = y3};
        Point p2_ = new Point{x = x2_, y = y2_};
        Point p3_ = new Point{x = x3_, y = y3_};
        List<Point> T1 = new List<Point>(){p1, p2, p3};
        List<Point> T2 = new List<Point>(){p1, p2_, p3_};
        return new List<List<Point>>(){T1, T2};
    }

    static public bool XFanSegmentIntersection(Stick Rotator, Stick Segment){
        var rotators = rotatorTriangles(Rotator);
        List<Point> T1 = rotators[0];
        List<Point> T2 = rotators[1];
        if (LineSegmentDistance.LineTriangleIntersect(T1, Segment.start, Segment.end) || LineSegmentDistance.LineTriangleIntersect(T2, Segment.start, Segment.end)){
            return true;
        }
        else{
            return false;
        }
    }

    static public bool XFanXFanIntersection(Stick Rotator1, Stick Rotator2){
        var rotators = rotatorTriangles(Rotator1);
        List<Point> T1 = rotators[0];
        List<Point> T2 = rotators[1];
        var rotators2 = rotatorTriangles(Rotator2);
        List<Point> T3 = rotators2[0];
        List<Point> T4 = rotators2[1];
        if (   LineSegmentDistance.TrianglesIntersect(T1, T3) 
            || LineSegmentDistance.TrianglesIntersect(T1, T4) 
            || LineSegmentDistance.TrianglesIntersect(T2, T3) 
            || LineSegmentDistance.TrianglesIntersect(T2, T4)){
            return true;
        }
        else{
            return false;
        }
    }

    static public bool SegmentSegmentDistanceWithinMinMax(Stick stick1, Stick stick2, double D_min, double D_max) {
        double distance = LineSegmentDistance.LineSegmentDist(stick1.start, stick1.end, stick2.start, stick2.end);
        return D_min <= distance && distance <= D_max;
    }

    static public bool CheckValidityOfNewStickWithOthers(List<Stick> sticks, Stick newStick, double D_min=cfg.sampleStickDistMin, double D_max=cfg.sampleStickDistMax){
        // include the check of the rotation validity
        double x = newStick.center.Item1;
        double y = newStick.center.Item2;
        double newlength = newStick.length;
        List<Stick> sorted = sticks.OrderBy(s => Math.Pow(s.center.Item1 - x, 2) + Math.Pow(s.center.Item2 - y, 2)).ToList();
        for(int i = 0; i < sorted.Count; i++){
            Stick stick = sorted[i];
            double x_ = stick.center.Item1;
            double y_ = stick.center.Item2;
            if (!SegmentSegmentDistanceWithinMinMax(stick, newStick, D_min, D_max)){
                return false;
            }
            if (newStick.isRotatable && stick.isRotatable && XFanXFanIntersection(stick, newStick)){
                return false;
            }
            if (newStick.isRotatable && !stick.isRotatable && XFanSegmentIntersection(newStick, stick)){
                return false;
            }
            if (!newStick.isRotatable && stick.isRotatable && XFanSegmentIntersection(stick, newStick)){
                return false;
            }

        }
        return true;
    }

    Stick SampleStickAtTopY(double W, double H,
                                double L_min=cfg.sampleStickLenMin, double L_max=cfg.sampleStickLenMax, 
                                List<double> angle_cands=null, double wall_dist=cfg.dist_wall_epsilon,
                                double isRotatableRate = cfg.rotational_stick_prob){
        if (angle_cands == null) angle_cands = cfg.sampleAngleCandidates;
        double angle = angle_cands[rand.Next(0, angle_cands.Count)];
        double radians = angle * (Math.PI / 180.0);
        double L = rand.NextDouble() * (L_max - L_min) + L_min;
        double dw = L * Math.Cos(radians);
        double dh = L * Math.Sin(radians);
        double L_w = Math.Abs(dw) + wall_dist;

        double x = rand.NextDouble() * (W - L_w) + (L_w / 2);

        Point start = new Point { x = x - dw / 2, y = H - dh / 2};
        Point end = new Point { x = x + dw / 2, y = H + dh / 2 };

        return new Stick { start = start, end = end, isRotatable = rand.NextDouble() < isRotatableRate};
    }

    public List<Stick> SampleSticks(double W, double H, int N_max = cfg.sampleStickMaxNum, 
                                    int attempt_up = cfg.sampleStickMaxTry, float y_step=cfg.attempt_y_step,
                                    float x_step=cfg.attempt_x_step) {
        // make odd even position of the cfg.sampleAngleCandidates 2 seperated lists
        List<double> angle_cands = cfg.sampleAngleCandidates;
        List<double> angle_cands_odd = new List<double>();
        List<double> angle_cands_even = new List<double>();
        for (int i = 0; i < angle_cands.Count; i++) {
            if (i % 2 == 0) angle_cands_even.Add(angle_cands[i]);
            else angle_cands_odd.Add(angle_cands[i]);
        }
        // consist two into one List<List<double>>
        List<List<double>> angle_cands_list = new List<List<double>>(){angle_cands_odd, angle_cands_even};
        // randomly choose one of the two lists
        int odd_even = rand.Next(0, 2);                
        // randomly generate origin Xs and sticks and iter to place the sticks until N attempts no new placed
        var stickPositions = new List<Stick>();
        int attempts = 0;
        int StepNum = (int)(H / y_step) + 1;
        while (stickPositions.Count < N_max && attempts <= attempt_up) {
            attempts += 1;
            Stick init_s = SampleStickAtTopY(W, H, angle_cands: angle_cands_list[odd_even]);
            bool ok = false;
            for (int i = 0; i<StepNum; i++){
                if (!CheckValidityOfNewStickWithOthers(stickPositions, init_s)){
                    init_s.move(0, -1 * y_step);
                }
                else{
                    ok = true;
                    break;
                }
            }
            if(ok){
                attempts=0;
                odd_even = 1 - odd_even;
                stickPositions.Add(init_s);
            }
        }
        // check each X by an internal to make sure all places are occupied in a uniform looking.
        var sampled = SampleStickAtTopY(W, H);
        double len = sampled.length;
        double dx = sampled.start.x - sampled.end.x;
        double dy = sampled.start.y - sampled.end.y;
        int XstepNum = (int)((W - len) / x_step) + 1;
        for (int i=0; i<XstepNum && stickPositions.Count <= N_max; i++){
            double x = i * x_step + len / 2;
            double y = sampled.center.Item2;
            bool ok = false;
            bool mirror = rand.NextDouble() < 0.5;
            Stick newStick = new Stick { start = new Point { x = x - dx / 2, y = y + (mirror?-1:+1) * dy / 2 }, 
                                            end = new Point { x = x + dx / 2, y = y + (mirror?1:-1) * dy / 2 }, isRotatable = false };
            for (int j = 0; j<StepNum; j++){
                if (!CheckValidityOfNewStickWithOthers(stickPositions, newStick)){
                    newStick.move(0, -1 * y_step);
                }
                else{
                    ok = true;
                    break;
                }
            }
            if(ok){
                stickPositions.Add(newStick);
            }
        }
        return stickPositions;
    }

}

public class Reservoir{ // reservoir actually
    public string name; //"Cup"named
    public int color;
    public Vector3 position;
    public Vector3 scale;
    public StickSampling.Stick stick1;
    public StickSampling.Stick stick2;
    public Dictionary<string, object> GetAll(){
        return new Dictionary<string, object>(){
            {"name", name},
            {"color", color},
            {"cupPosition", position.ToString()},
            {"cupScale", scale.ToString()},
            {"stickLeft", stick1.GetAll()},
            {"stickRight", stick2.GetAll()},
        };
    }
}

public class Fluid{
    public string name;
    public int color;
    public float density;
    public string viscosity;
    public float surfaceTension;
    public int amount;
    public Vector3 emitter_pos;
    public bool hasPredEmitter=false;
    public Vector3 pred_emitter_pos;
    public int pred_emitter_color;
    public Dictionary<string, object> GetAll(){
        return new Dictionary<string, object>(){
            {"name", name},
            {"color", color},
            {"density", density},
            {"viscosity", viscosity},
            {"viscositiesUnderTemperature", cfg.fluid_vis_temp_change_range[viscosity]},
            {"surfaceTension", surfaceTension},
            {"amount", amount},
            {"emitter_pos", emitter_pos.ToString()},
            {"hasPredEmitter", hasPredEmitter},
            {"pred_emitter_pos", pred_emitter_pos.ToString()},
            {"pred_emitter_color", pred_emitter_color},
        };
    }
}


public class FluidSlidesSampler : Sampler {
    // NOTE: 
    // 1. only Container is in world xyz, 
    //      others are in local xyz, 
    //      the point is the left bottom corner 
    //      of container
    public Dictionary<string, object> container;
    public List<StickSampling.Stick> sticks;
    public List<Reservoir> reservoirs;
    public List<Fluid> fluids;
    public FluidSlidesSampler(int seed) : base(seed) { 
        //pass
    }
    
    public string containerNameGetter(int color){
        return SoftColor.color_name_dict[color] + " Container";
    }
    
    public string fluidNameGetter(int color){
        return SoftColor.color_name_dict[color] + " Fluid";
    }

    public string stickNameGetter(int color, int theUp2DownSeq, int sameColorNum){
        if (sameColorNum == 1){
            return SoftColor.color_name_dict[color] + " Stick";
        }
        else if (sameColorNum == 2){
            if (theUp2DownSeq==0){
                return "the Right " + SoftColor.color_name_dict[color] + " Stick";
            }
            else{
                return "the Left " + SoftColor.color_name_dict[color] + " Stick";
            }
        }
        else{
            if (theUp2DownSeq <= sameColorNum / 2){
                if (theUp2DownSeq == 0)
                    return "the Rightmost " + SoftColor.color_name_dict[color] + " Stick";
                else
                    return "the " + SequenceNumberName.Num2Seq[theUp2DownSeq + 1] + " Rightmost " + SoftColor.color_name_dict[color] + " Stick";
            }
            else {
                if (theUp2DownSeq == sameColorNum - 1)
                    return "the Leftmost " + SoftColor.color_name_dict[color] + " Stick";
                else
                    return "the " + SequenceNumberName.Num2Seq[sameColorNum - theUp2DownSeq] + " Leftmost " + SoftColor.color_name_dict[color] + " Stick";
            }
        }
    }
    
    public void _Sample_Container(){
        container = new();
        var size = new List<float>(){
            (float)Sample_Conrange(cfg.container_size_range[0]),
            (float)Sample_Conrange(cfg.container_size_range[1]),
            (float)Sample_Conrange(cfg.container_size_range[2])};
        var centr = new List<float>(){
            (float)Sample_Conrange(cfg.container_loca_range[0]),
            (float)Sample_Conrange(cfg.container_loca_range[1]),
            (float)Sample_Conrange(cfg.container_loca_range[2])};
        float lowLimit = -cfg.receptor_h_relative * size[1];
        container.Add("size", size);
        container.Add("centre_location", centr);
        container.Add("lowerLimitY", lowLimit);
        // compute the wall place and scale:
        float pos_x_vert1 = centr[0] - size[0]/2 - cfg.wall_thickness/2 - cfg.stick_unpenetration_thickness_X;
        float pos_x_vert2 = centr[0] + size[0]/2 + cfg.wall_thickness/2 + cfg.stick_unpenetration_thickness_X;
        float pos_y_vert1 = centr[1] + lowLimit / 2 + cfg.stick_unpenetration_thickness_YUp / 2 - cfg.stick_unpenetration_thickness_YDown / 2; // no cfg.stick_unpenetration_thickness/ wall_thiickness because two sides are contrast
        float pos_y_vert2 = pos_y_vert1;
        float pos_x_hori1 = centr[0];
        float pos_x_hori2 = centr[0];
        float pos_y_hori1 = centr[1] - size[1]/2 + lowLimit - cfg.wall_thickness/2 - cfg.stick_unpenetration_thickness_YDown;
        float pos_y_hori2 = centr[1] + size[1]/2 + cfg.wall_thickness/2 + cfg.stick_unpenetration_thickness_YUp;
        float pos_z = centr[2];
        float scl_x_vert = cfg.wall_thickness;
        float scl_y_vert = 2 * (cfg.wall_thickness) + cfg.stick_unpenetration_thickness_YDown + cfg.stick_unpenetration_thickness_YUp - lowLimit + size[1];
        float scl_x_hori = size[0] + 2 * cfg.stick_unpenetration_thickness_X;
        float scl_y_hori = cfg.wall_thickness;
        float Ox = pos_x_vert1 + cfg.wall_thickness / 2 + cfg.stick_unpenetration_thickness_X;
        float Oy = centr[1] - size[1] / 2;
        float wall_thickness = cfg.wall_thickness;
        container.Add("wall_thickness", wall_thickness);
        container.Add("O_x", Ox);
        container.Add("O_y", Oy);
        container.Add("pos_x_vert1", pos_x_vert1);
        container.Add("pos_x_vert2", pos_x_vert2);
        container.Add("pos_y_vert1", pos_y_vert1);
        container.Add("pos_y_vert2", pos_y_vert2);
        container.Add("pos_x_hori1", pos_x_hori1);
        container.Add("pos_x_hori2", pos_x_hori2);
        container.Add("pos_y_hori1", pos_y_hori1);
        container.Add("pos_y_hori2", pos_y_hori2);
        container.Add("pos_z", pos_z);
        container.Add("scl_x_vert", scl_x_vert);
        container.Add("scl_y_vert", scl_y_vert);
        container.Add("scl_x_hori", scl_x_hori);
        container.Add("scl_y_hori", scl_y_hori);
        container.Add("scl_z", cfg.receptor_scale_z);
    }

    public void _Sample_Sticks(){
        sticks = new();
        StickSampling stick_sampling = new StickSampling(random.Next());
        var size = container["size"]as List<float>;
        var w = size[0];
        var h = size[1];
        sticks = stick_sampling.SampleSticks(w, h);
    }

    public void _Sample_Temperature(){
        int total_num = sticks.Count;
        int changeMatterNum = (int)(Sample_Conrange(cfg.stick_icyboil_range) * total_num);
        List<int> seq = Enumerable.Range(0, total_num).ToList();
        // shuffle the seq
        for (int i = 0; i < total_num; i++) {
            int j = random.Next(i, total_num);
            int temp = seq[i];
            seq[i] = seq[j];
            seq[j] = temp;
        }
        List<int> icyboil = seq.GetRange(0, changeMatterNum);
        for (int i = 0; i < total_num; i++) {
            string temperature;
            if (!icyboil.Contains(i))  temperature = Sample_Choice(cfg.stick_temperature);
            else temperature = Sample_Choice(cfg.stick_temperature_icy_boil);
            sticks[i].temperature = temperature;
        }
    }
    
    public bool y_Dist_Distinguishable(int id, List<int> toCheckLs, List<StickSampling.Stick> ls){
        var stick = ls[id];
        double y = stick.center.Item1;
        for (int i = 0; i < toCheckLs.Count; i++) {
            var stick_ = ls[toCheckLs[i]];
            if (Math.Abs(stick_.center.Item1 - y) < cfg.assign_stick_color_height_distinguish_thres) return false;
        }
        return true;
    }
    public void _Sample_Colors(){
        // another version of fewer sticks to have unique names
        sticks.Sort((x, y) => x.center.Item2.CompareTo(y.center.Item2));
        List<int> id_list = new List<int>();
        int this_clr_id = 0, this_obj_id=1;
        id_list.AddRange(Enumerable.Range(0, sticks.Count).ToList<int>());
        List<int> temp_clr_list = new List<int>(){id_list[0]};
        List<int> stick_clr = Sample_MultiChoice(cfg.stick_clr_range, cfg.stick_clr_range.Count);
        for (int i = 0; i < id_list.Count; i++) {
            int id = id_list[i];
            int color = stick_clr[i];
            sticks[id].color = color;
            sticks[id].name = stickNameGetter(color, 0, 1); // check i here is the correct order
        }
    }

    public void _Sample_Receptors(){
        reservoirs = new();
        var size = container["size"] as List<float>;
        var w = size[0] + 2 * cfg.stick_unpenetration_thickness_X;
        var h = size[1];
        int recptNum = Sample_Choice(cfg.receptor_num_range);
        List<int> color_pool = Sample_MultiChoice(cfg.receptor_clr_range, recptNum);
        float unit_x = w / recptNum;
        float start_x_from = unit_x / 2;
        float cup_offset_y = -cfg.receptor_h_relative * h;
        float cup_scale_x = cfg.receptor_w_relative * unit_x;
        float cup_scale_y = cfg.cup_h_relative * cfg.receptor_h_relative * h;
        for (int i = 0; i < recptNum; i++) {
            var recpt = new Reservoir();
            float cup_pos_y = cup_scale_y / 2 + cup_offset_y; // check the position
            float cup_pos_x = start_x_from + i * unit_x;
            recpt.position = new Vector3(cup_pos_x - cfg.stick_unpenetration_thickness_X, cup_pos_y - cfg.stick_unpenetration_thickness_YDown, 0);
            recpt.color = color_pool[i];
            recpt.name = containerNameGetter(recpt.color);
            recpt.scale = new Vector3(cup_scale_x, cup_scale_y, cfg.receptor_scale_z);
            // 2 sticks position and rotation
            float reachInPosX1 = cup_pos_x - cup_scale_x *(0.5f - cfg.receptor_scale_stickReachIn);
            float reachInPosX2 = cup_pos_x + cup_scale_x *(0.5f - cfg.receptor_scale_stickReachIn);
            float pos_stick_upper_x1 = i * unit_x;
            float pos_stick_upper_x2 = (i + 1) * unit_x;
            float pos_stick_upper_y = 0;
            float pos_stick_edge_x1 = cup_pos_x - cup_scale_x / 2;
            float pos_stick_edge_y = cup_pos_y + cup_scale_y / 2;
            float slope = (pos_stick_edge_y - pos_stick_upper_y) / (pos_stick_edge_x1 - pos_stick_upper_x1);
            float reachInPosY = pos_stick_upper_y + slope * (reachInPosX1 - pos_stick_upper_x1);
            recpt.stick1 = new StickSampling.Stick();
            recpt.stick2 = new StickSampling.Stick();
            recpt.stick1.start = new Point{ x = pos_stick_upper_x1 - cfg.stick_unpenetration_thickness_X, y = pos_stick_upper_y - cfg.stick_unpenetration_thickness_YDown};// subtract the avoid-penetration space
            recpt.stick1.end = new Point{ x = reachInPosX1 - cfg.stick_unpenetration_thickness_X, y = reachInPosY - cfg.stick_unpenetration_thickness_YDown};
            recpt.stick1.color = recpt.color;
            recpt.stick1.name = recpt.name + " Left Guided Stick";
            recpt.stick2.start = new Point{ x = pos_stick_upper_x2 - cfg.stick_unpenetration_thickness_X, y = pos_stick_upper_y - cfg.stick_unpenetration_thickness_YDown };
            recpt.stick2.end = new Point{ x = reachInPosX2 - cfg.stick_unpenetration_thickness_X, y = reachInPosY - cfg.stick_unpenetration_thickness_YDown };
            recpt.stick2.color = recpt.color;
            recpt.stick2.name = recpt.name + " Right Guided Stick";
            reservoirs.Add(recpt);
        }
    }

    public void _Sample_Fluids(){
        float width = (container["size"]as List<float>)[0];
        float height = (container["size"]as List<float>)[1];
        fluids = new();
        int num = Sample_Choice(cfg.fluid_num_range);
        int pred_num = Sample_Choice(cfg.pred_fluid_num_range);
        int emitter_num_all = num + pred_num;
        List<double> fluid_densities = new ();
        fluid_densities.AddRange(cfg.fluid_rho_list[num]);
        fluid_densities = fluid_densities.OrderBy(x => random.Next()).ToList(); // shuffle inject order
        List<int> pred_fluid_idx_list = Sample_MultiChoice(Enumerable.Range(0, num).ToList<int>(), pred_num);
        List<int> color_0 = Sample_MultiChoice(cfg.fluid_clr_range, num);
        List<float> emitter_pos_cache = new();
        float emitter_y = height + cfg.wall_thickness/2 + cfg.stick_unpenetration_thickness_YUp;
        int total_emitter_num = num;
        for (int i = 0; i < num; i++){
            if (pred_fluid_idx_list.Contains(i)){
                total_emitter_num += 1;
            }
        }
        bool undone = true;
        while (undone) {
            undone = false;
            emitter_pos_cache = new();
            for (int i = 0; i < total_emitter_num; i++){
                bool succeed_try = false;
                for (int k=0; k < 100; k++) {
                    float range1 = (float)random.NextDouble() * (width - cfg.fluid_emitter_width);
                    bool no_error = true;
                    foreach(float pos in emitter_pos_cache){
                        if (Math.Abs(pos - range1) < cfg.fluid_emitter_width){
                            no_error = false;
                            break;
                        }
                    }
                    if(no_error) {
                        emitter_pos_cache.Add(range1);
                        succeed_try = true;
                        break;
                    }
                }
                if (!succeed_try){
                    undone = true;
                    break;
                }
            }
        }
        int num_pos_ptr = 0;
        for (int i = 0; i < num; i++){
            Fluid fluid = new Fluid();
            fluid.amount = cfg.fluid_ptk_amount_total;
            fluid.color = color_0[i];
            fluid.density = (float)fluid_densities[i];
            fluid.viscosity = Sample_Choice(cfg.fluid_vis_types);
            fluid.surfaceTension = (float)Sample_Choice(cfg.fluid_sft_range);
            fluid.name = fluidNameGetter(fluid.color);
            fluid.emitter_pos = new Vector3(emitter_pos_cache[num_pos_ptr] + cfg.fluid_emitter_width / 2, emitter_y-cfg.wall_thickness/2, 0);
            num_pos_ptr += 1;
            if (pred_fluid_idx_list.Contains(i)){
                fluid.hasPredEmitter = true;
                fluid.pred_emitter_color = fluid.color;
                fluid.pred_emitter_pos = new Vector3(emitter_pos_cache[num_pos_ptr] + cfg.fluid_emitter_width / 2, emitter_y-cfg.wall_thickness/2, 0);
                num_pos_ptr += 1;
            }
            else{
                fluid.hasPredEmitter = false;
                fluid.pred_emitter_pos = new Vector3(0, 0, 0);
                fluid.pred_emitter_color = -1;
            }
            fluids.Add(fluid);
        }
        string type = fluids[0].viscosity;
        bool all_same = true;
        for (int i = 1; i < num; i++){
            if (type != fluids[i].viscosity){
                all_same = false;
                break;
            }
        }
        if (all_same){
            string target;
            do{
                target = Sample_Choice(cfg.fluid_vis_types);
            } while (type == target);
            fluids[(int)(num * (float)random.NextDouble())].viscosity = target;
        }
    }

    public void ResampleScene(){
        // clear the cache and resample
        _Sample_Container();
        _Sample_Sticks();
        _Sample_Colors();
        _Sample_Temperature();
        _Sample_Receptors();
        _Sample_Fluids();
    }

    public Dictionary<string, object> GetAll(){
        return new Dictionary<string, object>(){
            {"container", container},
            {"sticks", sticks.Select(s => s.GetAll()).ToList()},
            {"reservoirs", reservoirs.Select(r => r.GetAll()).ToList()},
            {"fluids", fluids.Select(f => f.GetAll()).ToList()},
        };        
    }

}