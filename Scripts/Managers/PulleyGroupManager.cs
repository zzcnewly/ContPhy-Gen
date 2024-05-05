// is added to perception camera version 3.1 dirived from baseManager
// ffmpeg -framerate 60 -i step%d.camera.png  -start_number 5 -to 200 -c:v libx264 -profile:v high -crf 20 -pix_fmt yuv420p output.mp4
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
using UnityEngine.Perception.GroundTruth.LabelManagement;
using UnityEditor.VersionControl;
using UnityEngine.UIElements;
using UnityEngine.Perception.GroundTruth.Labelers;
using Filo;
using UnityEngine.Rendering;
using System.Numerics;
using UnityEditor.Experimental.GraphView;
using JetBrains.Annotations;
using UnityEditor.Animations;
using Palmmedia.ReportGenerator.Core.Reporting.Builders;
using Unity.Collections;
using UnityEditor.Rendering.LookDev;
using Unity.Services.Core.Environments;
using System.Security.Cryptography;
using System.Globalization;
using UnityEditorInternal.Profiling.Memory.Experimental;
using PulleyGroupConfig;
using PlacementConfig;
using UnityEditor.UI;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Profiling;
using PRUtils;
using Unity.Collections.LowLevel.Unsafe;
using Unity.VisualScripting.ReorderableList;
using System.Threading;
using UnityEditor.TerrainTools;
using UnityEditor.Rendering;
using UnityEditor.Media;
using UnityEngine.Android;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.Perception.GroundTruth.DataModel;
using Unity.Mathematics;
using System.ComponentModel.Design;

[RequireComponent(typeof(Camera))]
public class PulleyGroupRandomizerTag : RandomizerTag {
}


[Serializable]
[AddRandomizerMenu("Pulley Group Manager")]
public class PulleyGroupManager : BaseManager
{

    // Parameters and Caches
    public GameObject _light;
    public IntegerParameter pulley_mode = new() { value = new UniformSampler(0, 0) };
    public IntegerParameter layer_num = new() { value = new UniformSampler(cfg.layer_num_min, cfg.layer_num_max) };
    public IntegerParameter column_num_1l = new() { value = new UniformSampler(cfg.column_num_min_1layer, cfg.column_num_max_1layer) };// basic random parameter
    public IntegerParameter column_num_2l = new() { value = new UniformSampler(cfg.column_num_min_2layer, cfg.column_num_max_2layer) };// basic random parameter
    public IntegerParameter element_num = new() { value = new UniformSampler(cfg.element_num_min, cfg.element_num_max) };// basic random parameter
    public UnityEngine.Perception.Randomization.Parameters.FloatParameter rand_01 = new() { value = new UniformSampler(0f, 1.0f) };
    
    
    public   int answer_refered_start_frame; // leave rest time for balance

    public bool done;
    public bool mirrored = false; 
    public int mode = 0;
    public int layer_n = 0;
    public List<int> col_n = new();
    public List<int> element_n = new();
    public List<List<GameObject>> Chains = new();
    public List<GameObject> Ropes = new();
    public List<(GameObject, int)> Cubes = new();
    public List<int> color_candidates;
    public int color_id = 0, color_id0 = 0, color_id1 = 0, color_id2 = 0, color_idr = 0, color_idcub = 0, color_idsph = 0, color_idlargedyn = 0;
    public Dictionary<string, int> RotationAnswers = new(); // CW or ACW or None 1 -1 0
    public Dictionary<string, int> MotionAnswers = new(); // up 1 down -1 none 0
    public Dictionary<string, Dictionary<(string, string), float>> Tension = new(); // each rope: tension between A and B
    public Dictionary<string, float> TensionAvg = new(); // each rope: tension between A and B
    // AnswerRelations:
    // connected ones are in one List
    public List<List<string>> AnswerRelations = new();
    public List<Dictionary<string, List<string>>> AnswerRelationsInRope = new();
    public List<Dictionary<string, string>> QuestionMovingAgentAll = new();
    public Dictionary<string, string> QuestionMovingAgent = new();
    public (string, float, float) QuestionMassChange_ColorMassBA = new();
    public List<float> questionMovingAgentPosition = new ();
    // states:
    // state, position, x, y, appearance, 
    // ( end-dyn-end have rope division id ) 
    public List<List<Dictionary<string, int>>> states = new();
    // poses:
    // type, position(if is_pulley), real-x, y, z, idx_in_states, idy_in_states, 
    // idx, idy, rx, ry, rz, rw, scale: big/little, color
    // (target_x/y, check_id)
    public List<List<Dictionary<string, float>>> poses = new();
    public Dictionary<string, int> ropeColor = new();
    public List<int> connRopeColor = new();
    // connects:
    // x(idx in list), y, type: 1puly-puly
    public List<Dictionary<string, int>> connects = new(); 
    // divisions:
    // end types: ceil, floor, load, puly, 
    // end position: x,y
    // END_DYNAMIC_PULY: target_x, target_y
    // END_DYNAMIC_END: target_division_id
    public List<Dictionary<string, int>> divisions = new(); 
    public List<List<int>> dynamics = new();

    // cf settings
    int this_move = 0, this_mass_change = 0;
    List<string> temp_static;

    Dictionary<string, List<int>> name2idsInPose;
    Dictionary<string, UnityEngine.Vector2> name2position;

    public void SampleScheme() {
        mode = pulley_mode.Sample();
        if (mode == 0){ // 2D pulley
            // initialize
            layer_n = layer_num.Sample();
            col_n = new();
            element_n = new();
            states = new();
            poses = new();
            ropeColor = new();
            connRopeColor = new();
            connects = new();
            divisions = new();
            dynamics = new();
            Chains = new();
            Ropes = new();
            Cubes = new();
            int depth = 0;
            int ptr_0 = 0;
            int last_uplw = cfg.UPPER;
            int next_xid = 0;
            int last_xid = 0;
            int last_depth = 0;
            int this_rope_eles_total = 0;
            int this_rope_eles = 0;
            bool done = false;

            List<Dictionary<string, int>> state;
            Dictionary<string, int> s;
            while(true) {
                Debug.Log("depth: " + depth);
                int xid_step = depth % 2 == 0 ? 1 : -1;
                int cols = layer_n == 0? column_num_1l.Sample():column_num_2l.Sample(); // 1 or 2 layers have different column numbers
                if (depth == 1 && cols < this_rope_eles) { // all done  // 1 or 2 layers have different column numbers
                    cols = this_rope_eles; // avoid too great layers more than 2 // avoid transmission
                }
                col_n.Add(cols); 
                state = new();
                for (int i = 0; i < cols; i++){
                    if ( this_rope_eles == 0 ){ // rope is over
                        this_rope_eles = element_num.Sample();
                        if (depth == layer_n && this_rope_eles + i >= cols) { // all done
                            this_rope_eles = cols - i;
                        }
                        if (depth > layer_n /*|| (depth == layer_n && this_rope_eles + i >= cols)*/) { // all done
                            done = true;
                            break;
                        }
                        this_rope_eles_total = this_rope_eles;
                        element_n.Add(this_rope_eles);
                        // start this rope
                        float samp = rand_01.Sample();
                        if (samp < cfg.up_start_p) { // upper
                            s = new(); s.Add("type", cfg.END_STATIC_CEIL); s.Add("x", next_xid); s.Add("y", depth); 
                            divisions.Add(s);
                            last_uplw = cfg.UPPER;
                        }
                        else if (samp - cfg.up_start_p < cfg.low_start_p) { // lower
                            s = new(); s.Add("type", cfg.END_STATIC_FLOO); s.Add("x", next_xid); s.Add("y", depth);
                            divisions.Add(s);
                            last_uplw = cfg.LOWER;
                        }
                        else { // load end
                            // // this should be handled when detected. 
                            s = new(); s.Add("type", cfg.END_DYNAMIC_WAIT); s.Add("x", next_xid); s.Add("y", depth);
                            divisions.Add(s);
                            s = new(); s.Add("state", cfg.END_DYNAMIC_WAIT); s.Add("position", cfg.LOWER_DYN); s.Add("x", next_xid); s.Add("y", depth);
                            s.Add("division_id", divisions.Count - 1);
                            state.Add(s);
                            next_xid += xid_step;
                            last_uplw = cfg.LOWER_DYN;
                        }
                    }

                    ptr_0 -= 1;
                    if (cfg.avoid_unstable_layout && ( i == 0 || i == cols - 1)){
                        float prob = rand_01.Sample();
                        if (prob < 0.5f){
                            last_uplw = cfg.UPPER_;
                            s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.UPPER_); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                            state.Add(s);
                        }
                        else {
                            last_uplw = cfg.LOWER_;
                            s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.LOWER); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                            state.Add(s);
                        }
                    }
                    else if (last_uplw == cfg.UPPER || last_uplw == cfg.UPPER_ || last_uplw == cfg.UPPER_DYN){ // INPUT UPPER Static
                        float prob = rand_01.Sample();
                        if ((last_uplw == cfg.UPPER && prob < cfg.sta_flat_p)){ // output flat UPPER
                            last_uplw = cfg.UPPER_;
                            s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.UPPER_); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                            state.Add(s);
                        }

                        else { // output low
                            if (rand_01.Sample() < cfg.low_sta_p || (cfg.avoid_unstable_layout && last_uplw == cfg.UPPER_DYN)){ // output lower static
                                last_uplw = cfg.LOWER;
                                s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.LOWER); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                state.Add(s);
                            }
                            else { // output lower dynamic
                                last_uplw = cfg.LOWER_DYN;
                                if (rand_01.Sample() < cfg.low_load_p) { // add lower with load
                                    s = new(); s.Add("state", cfg.DYNAMIC_WITH_LOAD); s.Add("position", cfg.LOWER_DYN); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                    state.Add(s);
                                }
                                else { // waiting for connection else add load
                                    s = new(); s.Add("state", cfg.DYNAMIC_WAIT); s.Add("position", cfg.LOWER_DYN); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                    state.Add(s);
                                }
                            }
                        }
                    }

                    else if(last_uplw == cfg.LOWER || last_uplw == cfg.LOWER_ || last_uplw == cfg.LOWER_DYN) {// INPUT lower Static
                        float prob = rand_01.Sample();

                        if (last_uplw == cfg.LOWER && prob < cfg.sta_flat_p) { // output flat lower
                            last_uplw = cfg.LOWER_;
                            s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.LOWER_); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                            state.Add(s);
                        }


                        else { // output upper
                            if (depth > 0 && ptr_0 >= 0 && (states[depth - 1][ptr_0]["position"] == cfg.LOWER_DYN) 
                                && ( states[depth - 1][ptr_0]["state"] == cfg.DYNAMIC_WAIT || states[depth - 1][ptr_0]["state"] == cfg.END_DYNAMIC_WAIT )
                                && (!(cfg.avoid_unstable_layout && last_uplw == cfg.LOWER_DYN))) // avoid too unstable situation 
                            { // upper pulley is waiting for connection
                                if (states[depth - 1][ptr_0]["state"] == cfg.DYNAMIC_WAIT){ // above is dynamic pulley 
                                    float up_prob = rand_01.Sample();
                                    if (up_prob < cfg.up_with_upper_dyn_static_p){ // output static and ignore upper pulley
                                        last_uplw = cfg.UPPER;
                                        s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.UPPER); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                        state.Add(s);
                                        states[depth - 1][ptr_0]["state"] = cfg.DYNAMIC_WITH_LOAD;
                                    }
                                    else { // output dyn
                                        if (this_rope_eles_total > this_rope_eles && up_prob - cfg.up_with_upper_dyn_static_p < cfg.up_with_upper_dyn_end_p) { // output dyn end connect to upper puly
                                            this_rope_eles = 1;
                                            last_uplw = cfg.END_DYNAMIC_PULY;
                                            states[depth - 1][ptr_0]["state"] = cfg.DYNAMIC_WITH_END;
                                            s = new(); s.Add("type", cfg.END_DYNAMIC_PULY); s.Add("x", last_xid); s.Add("y", last_depth);
                                            s.Add("target_x", states[depth - 1][ptr_0]["x"]); 
                                            s.Add("target_y", states[depth - 1][ptr_0]["y"]);
                                            divisions.Add(s);
                                        }
                                        else {// output dyn pulley
                                            states[depth - 1][ptr_0]["state"] = cfg.DYNAMIC_WITH_DYN;
                                            last_uplw= cfg.UPPER_DYN;
                                            s = new(); s.Add("state", cfg.DYNAMIC_WITH_DYN); s.Add("position", cfg.UPPER_DYN); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                            state.Add(s);
                                            // new puly-puly connection!
                                            s = new(); s.Add("x1", states[depth - 1][ptr_0]["x"]); s.Add("x2", next_xid); s.Add("y1", states[depth - 1][ptr_0]["y"]); s.Add("y2", depth);
                                            connects.Add(s);
                                        }
                                    }
                                }


                                else { // up with upper end waiting
                                    float up_prob = rand_01.Sample();
                                    if (up_prob < cfg.up_with_upper_dyn_static_p) { // output sta
                                        last_uplw = cfg.UPPER;
                                        states[depth - 1][ptr_0]["state"] = cfg.END_DYNAMIC_LOAD;
                                        s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.UPPER); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                        state.Add(s);
                                        divisions[states[depth - 1][ptr_0]["division_id"]]["type"] = cfg.END_DYNAMIC_LOAD;
                                    }
                                    else { // output dyn under end above
                                        if (this_rope_eles_total > this_rope_eles && up_prob - cfg.up_with_upper_dyn_static_p < cfg.up_with_upper_dyn_end_p) { // output dyn end
                                            this_rope_eles = 1;
                                            states[depth - 1][ptr_0]["state"] = cfg.END_DYNAMIC_END;
                                            // Connect 2 Ropes
                                            int id = states[depth - 1][ptr_0]["division_id"];
                                            divisions[id]["type"] = cfg.END_DYNAMIC_END;
                                            divisions[id].Add("target_division_id", divisions.Count);
                                            s = new(); s.Add("type", cfg.END_DYNAMIC_END); s.Add("x", last_xid); s.Add("y", last_depth);
                                            s.Add("target_division_id", id); 
                                            divisions.Add(s);
                                            last_uplw = cfg.END_DYNAMIC_END;
                                        }
                                        else {// output dyn pulley
                                            states[depth - 1][ptr_0]["state"] = cfg.END_DYNAMIC_PULY;
                                            int id = states[depth - 1][ptr_0]["division_id"];
                                            last_uplw= cfg.UPPER_DYN;
                                            divisions[id]["type"] = cfg.END_DYNAMIC_PULY;
                                            divisions[id].Add("target_x", next_xid);
                                            divisions[id].Add("target_y", depth);
                                            
                                            s = new(); s.Add("state", cfg.DYNAMIC_WITH_END); s.Add("position", cfg.UPPER_DYN); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                            state.Add(s);
                                        }
                                    }
                                }
                                
                            }
                            else {
                                // up wo dyn: must be static or end
                                s = new(); s.Add("state", cfg.STATICS); s.Add("position", cfg.UPPER); s.Add("appearance", rand_01.Sample() < cfg.big_pulley_p? cfg.BIG:cfg.LITTLE); s.Add("x", next_xid); s.Add("y", depth);
                                state.Add(s);
                                last_uplw = cfg.UPPER;
                            }
                        }
                    }
                    else {
                        Debug.Log("Error: last_uplw is not valid");
                    }
                    last_xid = next_xid;
                    last_depth = depth;
                    next_xid += xid_step;
                    this_rope_eles -= 1;
                    // end the latest rope
                    if (this_rope_eles == 0){
                        if (last_uplw == cfg.END_DYNAMIC_END || last_uplw == cfg.END_DYNAMIC_PULY){
                            // pass
                        }
                        else if (last_uplw == cfg.UPPER || last_uplw == cfg.UPPER_ || last_uplw == cfg.UPPER_DYN) { // lower end
                            float samp = rand_01.Sample();
                            if (samp < cfg.low_end_sta_p) { // lower floor
                                s = new(); s.Add("type", cfg.END_STATIC_FLOO); s.Add("x", last_xid); s.Add("y", last_depth);
                                divisions.Add(s); 
                            }
                            else if (samp - cfg.low_end_sta_p < cfg.low_end_load_p) { // lower load
                                s = new(); s.Add("type", cfg.END_DYNAMIC_LOAD); s.Add("x", last_xid); s.Add("y", last_depth);
                                divisions.Add(s); 
                            }
                            else { // waiting for connection otherwise add load
                                s = new(); s.Add("type", cfg.END_DYNAMIC_WAIT); s.Add("x", last_xid); s.Add("y", last_depth);
                                divisions.Add(s);
                                s = new(); s.Add("state", cfg.END_DYNAMIC_WAIT); s.Add("position", cfg.LOWER_DYN); s.Add("x", next_xid); s.Add("y", depth);
                                s.Add("division_id", divisions.Count - 1);
                                state.Add(s);
                                next_xid += xid_step;
                            }
                        }
                        else { // upper end --ceiling // TODO add dynamic detection
                            s = new(); s.Add("type", cfg.END_STATIC_CEIL); s.Add("x", last_xid); s.Add("y", last_depth); 
                            divisions.Add(s);
                        }
                    }
                }
                last_depth = depth;
                next_xid -= xid_step; // return one step because it's moving down
                if (done) { if (state.Count > 0) {states.Add(state);} break; }
                states.Add(state);
                ptr_0 = state.Count;
                depth += 1;
            }
            Debug.Log("states.Count: " + states.Count + " divisions.Count: " + states[0].Count); 
        }
        else if (mode == 1){ // 3D pulley
            // pass
        }
    }
    
    public Dictionary<string, bool> object_up_down;

    public void SequenceScheme() {
        // from the SampleScheme get the sequence of states for next Phase
        if (mode == 0){
            poses = new(); // each list is a complete rope
            color_id = 0; color_id0 = 0; color_id1 = 0; color_id2 = 0; color_idr = 0; color_idcub = 0; color_idsph = 0; color_idlargedyn = 0;

            // load // dyn pulley color //dyn little pulley
            color_candidates = new ();
            color_candidates.AddRange(cfg.load_color_range);
            // shuffle the color list
            for (int i = 0; i < color_candidates.Count; i++) {
                int temp = color_candidates[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates.Count);
                color_candidates[i] = color_candidates[randomIndex];
                color_candidates[randomIndex] = temp;
            }
            color_candidates.AddRange(cfg.load_color_range_plus);

            // dyn large pulley
            List<int> color_candidates_l = new();
            color_candidates_l.AddRange(cfg.load_color_range);
            // shuffle the color list
            for (int i = 0; i < color_candidates_l.Count; i++) {
                int temp = color_candidates_l[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates_l.Count);
                color_candidates_l[i] = color_candidates_l[randomIndex];
                color_candidates_l[randomIndex] = temp;
            }
            color_candidates_l.AddRange(cfg.load_color_range_plus);

            // cube color
            List<int> color_candidates_cub = new();
            color_candidates_cub.AddRange(cfg.load_color_range);
            // shuffle the color list
            for (int i = 0; i < color_candidates_cub.Count; i++) {
                int temp = color_candidates_cub[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates_cub.Count);
                color_candidates_cub[i] = color_candidates_cub[randomIndex];
                color_candidates_cub[randomIndex] = temp;
            }
            color_candidates_cub.AddRange(cfg.load_color_range_plus);

            // sphere color
            List<int> color_candidates_sph = new();
            color_candidates_sph.AddRange(cfg.load_color_range);
            // shuffle the color list
            for (int i = 0; i < color_candidates_sph.Count; i++) {
                int temp = color_candidates_sph[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates_sph.Count);
                color_candidates_sph[i] = color_candidates_sph[randomIndex];
                color_candidates_sph[randomIndex] = temp;
            }
            color_candidates_sph.AddRange(cfg.load_color_range_plus);

            // small pulley color     
            List<int> color_candidates0 = new List<int>();
            color_candidates0.AddRange(cfg.disk_color_range);
            // shuffle the list
            for (int i = 0; i < color_candidates0.Count; i++) {
                int temp = color_candidates0[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates0.Count);
                color_candidates0[i] = color_candidates0[randomIndex];
                color_candidates0[randomIndex] = temp;
            }
            color_candidates0.AddRange(cfg.load_color_range_plus);

            // large pulley color
            List<int> color_candidates1 = new List<int>();
            color_candidates1.AddRange(cfg.disk_color_range);
            // shuffle the list
            for (int i = 0; i < color_candidates1.Count; i++) {
                int temp = color_candidates1[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates1.Count);
                color_candidates1[i] = color_candidates1[randomIndex];
                color_candidates1[randomIndex] = temp;
            }
            color_candidates1.AddRange(cfg.load_color_range_plus);

            // static
            List<int> color_candidates2 = new List<int>();
            color_candidates2.AddRange(cfg.disk_color_range);
            // shuffle the list
            for (int i = 0; i < color_candidates2.Count; i++) {
                int temp = color_candidates2[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates2.Count);
                color_candidates2[i] = color_candidates2[randomIndex];
                color_candidates2[randomIndex] = temp;
            }
            color_candidates2.AddRange(cfg.load_color_range_plus);

            // rope colors
            List<int> color_candidates_r = new List<int>();
            color_candidates_r.AddRange(cfg.rope_color_range);
            // shuffle the list
            for (int i = 0; i < color_candidates_r.Count; i++) {
                int temp = color_candidates_r[i];
                int randomIndex = UnityEngine.Random.Range(i, color_candidates_r.Count);
                color_candidates_r[i] = color_candidates_r[randomIndex];
                color_candidates_r[randomIndex] = temp;
            }
            color_candidates_r.AddRange(cfg.load_color_range_plus);
            
            int ptr_x = 0, ptr_y = 0;
            for (int i = 0; i < divisions.Count; i += 2){
                List<Dictionary<string, float>> rope_element_pose = new();
                Dictionary<string, int> start = divisions[i];
                int start_x = start["x"], start_y = start["y"];
                Dictionary<string, int> end = divisions[i + 1];
                int end_x = end["x"], end_y = end["y"];
                bool is_over = false;
                while ( !is_over ) {
                    Debug.Log("is_over");
                    Dictionary<string, float> p = new();
                    Dictionary<string, int> this_state = states[ptr_y][ptr_x];
                    int this_x = this_state["x"];
                    int this_y = this_state["y"];
                    int this_p = this_state["position"];
                    if (this_state["state"] <= 3) {
                        p.Add("type", this_state["state"]);
                        p.Add("x", cfg.base_x + this_x * cfg.x_step);
                        p.Add("y",  cfg.base_y - (this_p % 2 == 0 ? 0 : 1) * cfg.y_step - 2 * this_y * cfg.layer_step);
                        p.Add("z", 0);
                        p.Add("idx", this_x);
                        p.Add("idy", this_y);
                        p.Add("idx_in_states", ptr_x);
                        p.Add("idy_in_states", ptr_y);
                        p.Add("scale", this_state["appearance"]);
                        p.Add("position", this_p);
                        if (this_state["state"] == cfg.END_DYNAMIC_LOAD || this_state["state"] == cfg.DYNAMIC_WITH_LOAD 
                            || this_state["state"] == cfg.DYNAMIC_WAIT || this_state["state"] == cfg.DYNAMIC_WITH_DYN 
                            || this_state["state"] == cfg.DYNAMIC_WITH_END) {
                            if (this_state["state"] == cfg.DYNAMIC_WITH_LOAD || this_state["state"] == cfg.DYNAMIC_WAIT){
                                string shape = (rand_01.Sample() < 0.5) ? "Cube" : "Sphere"; 
                                p.Add("loadShape", shape == "Cube" ? 1 : 0);
                                if (shape == "Cube"){
                                    if (color_idcub < color_candidates_cub.Count) 
                                        p.Add("loadColor", color_candidates_cub[color_idcub]);
                                    else
                                        p.Add("loadColor", color_candidates_cub[0]);
                                    color_idcub += 1;
                                }
                                else{
                                    if (color_idsph < color_candidates_sph.Count) 
                                        p.Add("loadColor", color_candidates_sph[color_idsph]);
                                    else
                                        p.Add("loadColor", color_candidates_sph[0]);
                                    color_idsph += 1;
                                }
                            }
                            if (this_state["appearance"] == cfg.BIG){
                                if (color_idlargedyn < color_candidates_l.Count) 
                                    p.Add("color", color_candidates_l[color_idlargedyn]);
                                else
                                    p.Add("color", color_candidates_l[0]);
                                color_idlargedyn += 1;
                            }
                            else {
                                if (color_id < color_candidates.Count) 
                                    p.Add("color", color_candidates[color_id]);
                                else
                                    p.Add("color", color_candidates[0]);
                                color_id += 1;
                            }
                            
                        }
                        else if (this_state["state"] == cfg.STATICS){
                            if (this_state["appearance"] == cfg.BIG){
                                if (color_id1 < color_candidates1.Count) 
                                    p.Add("color", color_candidates1[color_id1]);
                                else
                                    p.Add("color", color_candidates1[0]);
                                color_id1 += 1;
                            }
                            else {
                                if (color_id0 < color_candidates0.Count) 
                                    p.Add("color", color_candidates0[color_id0]);
                                else
                                    p.Add("color", color_candidates0[0]);
                                color_id0 += 1;
                            }
                        }
                        else if (this_state["state"] == cfg.END_STATIC_CEIL 
                                 || this_state["state"] == cfg.END_STATIC_FLOO){
                            if (color_id2 < color_candidates2.Count) 
                                p.Add("color", color_candidates2[color_id2]);
                            else
                                p.Add("color", color_candidates2[0]);
                            color_id2 += 1;
                        }
                        else {
                            Debug.Log("What is this");
                        }
                        rope_element_pose.Add(p);
                    }
                    ptr_x += 1;
                    if (ptr_x == states[ptr_y].Count) {
                        ptr_x = 0; ptr_y += 1;
                    }
                    if (end_x == this_x && end_y == this_y) {
                        
                        is_over = true;
                    }
                }
                poses.Add(rope_element_pose);
            }

            // check divisions
            Dictionary<int, int> _id_pool = new();
            int check_id_pool = 0;
            for (int i = 0; i < divisions.Count; i += 1){
                Dictionary<string, int> term = divisions[i];
                int term_type = term["type"] == cfg.END_DYNAMIC_WAIT ? cfg.END_DYNAMIC_LOAD : term["type"];
                int term_x = term["x"];
                int term_y = term["y"];
                Dictionary<string, float> p = new();
                p.Add("type", term_type); 
                if (term_type == cfg.END_STATIC_CEIL || term_type == cfg.END_STATIC_FLOO || term_type == cfg.END_DYNAMIC_LOAD || term_type == cfg.END_DYNAMIC_PULY) {
                    int dir = (term_y % 2 == 0 ? -1 : 1) * (i % 2 == 0 ? 1 : -1);
                    Dictionary<string, float> temp = poses[(int)(i / 2)][i % 2 == 0 ? 0 : poses[(int)(i / 2)].Count - 1];
                    p.Add("x", temp["x"] + 0.5f * cfg.x_step * dir * cfg.radius_scale);
                    float temp_y = temp["y"] + ((int)temp["position"] % 2 == 0 ? -1 : 1) * 0.5f * cfg.y_step;
                    if (term_type == cfg.END_STATIC_CEIL) {
                        temp_y += cfg.y_step * cfg.ceil_floor_scale;
                    }
                    else if (term_type == cfg.END_STATIC_FLOO) {
                        temp_y -= cfg.y_step * cfg.ceil_floor_scale;
                    }
                    else {
                        temp_y = temp["y"] - cfg.obj_pos_scale * cfg.y_step;
                    }
                    p.Add("y", temp_y);
                    p.Add("z", 0);
                    p.Add("position", temp["position"]);
                    if (term_type == cfg.END_DYNAMIC_PULY) {
                        p.Add("target_x", term["target_x"]);
                        p.Add("target_y", term["target_y"]);
                    }
                    if (term_type == cfg.END_DYNAMIC_LOAD) {
                        string shape = (rand_01.Sample() < 0.5) ? "Cube" : "Sphere"; 
                        p.Add("loadShape", shape == "Cube" ? 1 : 0);
                        if (shape == "Cube") {
                            if (color_idcub < color_candidates_cub.Count) 
                                p.Add("color", color_candidates_cub[color_idcub]);
                            else
                                p.Add("color", color_candidates_cub[0]);
                            color_idcub += 1;
                        }
                        else {
                            if (color_idsph < color_candidates_sph.Count) 
                                p.Add("color", color_candidates_sph[color_idsph]);
                            else
                                p.Add("color", color_candidates_sph[0]);
                            color_idsph += 1;
                        }
                    }
                    else if (term_type == cfg.END_STATIC_CEIL 
                             || term_type == cfg.END_STATIC_FLOO){
                        if (color_id2 < color_candidates2.Count) 
                            p.Add("color", color_candidates2[color_id2]);
                        else
                            p.Add("color", color_candidates2[0]);
                        color_id2 += 1;
                    }
                }
                else if (term_type == cfg.END_DYNAMIC_END) {
                    int checkid = -1; // add labels for merging
                    if (_id_pool.TryGetValue(term["target_division_id"], out checkid)){
                        p.Add("check_id", checkid);
                    }
                    else{
                        _id_pool.Add(i, check_id_pool);
                        p.Add("check_id", check_id_pool);
                        check_id_pool++;
                    }
                }
                else {
                    Debug.Log("Unknown term type");
                    throw new Exception("Unknown term type");
                }
                poses[(int)(i / 2)].Insert(i % 2 == 0 ? 0 : poses[(int)(i / 2)].Count, p);
            }

            // merging
            bool checking = true;
            while (checking){
                Debug.Log("Merging");
                checking = false;
                int label = -1;
                for (int i = 0; i < poses.Count; i += 1){
                    if (poses[i][0]["type"] == cfg.END_DYNAMIC_END || poses[i][poses[i].Count - 1]["type"] == cfg.END_DYNAMIC_END){
                        checking = true;
                        if (poses[i][0]["type"] == cfg.END_DYNAMIC_END && poses[i][poses[i].Count - 1]["type"] == cfg.END_DYNAMIC_END){
                            // LOOP
                            poses[i][0]["type"] = cfg.END_DYNAMIC_END_LOOP;
                            poses[i][poses[i].Count - 1]["type"] = cfg.END_DYNAMIC_END_LOOP;
                            continue;
                        }
                        bool left = poses[i][0]["type"] == cfg.END_DYNAMIC_END;
                        label = left ? (int)poses[i][0]["check_id"] : (int)poses[i][poses[i].Count - 1]["check_id"];
                        if (left) { // turn right 
                            poses[i] = Enumerable.Reverse(poses[i]).ToList();
                        }
                        for (int j = i + 1; j < poses.Count; j += 1){
                            if (poses[j][0]["type"] == cfg.END_DYNAMIC_END || poses[j][poses[j].Count - 1]["type"] == cfg.END_DYNAMIC_END){
                                bool right = poses[j][poses[j].Count - 1]["type"] == cfg.END_DYNAMIC_END;
                                if ((right ? poses[j][poses[j].Count - 1]["check_id"] : poses[j][0]["check_id"]) == label) {
                                    if (right) {
                                        poses[j] = Enumerable.Reverse(poses[j]).ToList();
                                    }
                                    poses[i].RemoveAt(poses[i].Count - 1);
                                    poses[j].RemoveAt(0);
                                    poses[i].AddRange(poses[j]);
                                    poses.RemoveAt(j);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // control dynamic nodes
            if (cfg.keep_2_dyn_nodes_per_cable) {
                dynamics = new List<List<int>>();
                List<int> dynamic_nodes;
                for (int rope_id = 0; rope_id < poses.Count; rope_id += 1) {
                    dynamic_nodes = new ();
                    for (int i = 1; i < poses[rope_id].Count - 1; i += 1) {
                        if (poses[rope_id][i]["type"] < 3) {
                            dynamic_nodes.Add(i);
                        }
                    }
                    if (poses[rope_id][0]["type"] <= 9 && poses[rope_id][0]["type"] >= 6) {
                        dynamic_nodes.Add(0);
                    }
                    if (poses[rope_id][poses[rope_id].Count-1]["type"] <= 9 && poses[rope_id][poses[rope_id].Count-1]["type"] >= 6) {
                        dynamic_nodes.Add(poses[rope_id].Count-1);
                    }
                    // randomly solidify some dynamic nodes
                    for (int p = 0; (dynamic_nodes.Count > cfg.max_dyn_nodes_per_cable) && p < cfg.element_num_max * 10; p++){
                        int index = ((int)Math.Floor(rand_01.Sample() * (dynamic_nodes.Count)));
                        int index_ = dynamic_nodes[index];
                        if (poses[rope_id][index_]["type"] == cfg.END_DYNAMIC_PULY) continue;
                        else if (index_ == 0 || index_ == poses[rope_id].Count - 1) {
                            poses[rope_id][index_]["type"] = (int)poses[rope_id][index_]["position"] / 2 == 0 ? cfg.END_STATIC_FLOO : cfg.END_STATIC_CEIL; // do not mind it's ceil or floor, we have the x/y
                            // modify the position of the new static node for appearance.
                            float pose_y = poses[rope_id][index_]["y"] + cfg.obj_pos_scale * cfg.y_step  // pulley position
                                            + ((int)poses[rope_id][index_]["position"] % 2 == 0 ? -1 : 1) * 0.5f * cfg.y_step
                                            + ((int)poses[rope_id][index_]["position"] / 2 == 0 ? -1 : 1) * cfg.ceil_floor_scale * cfg.y_step;
                            poses[rope_id][index_]["y"] = pose_y;
                            // Modify color
                            if (color_id2 < color_candidates2.Count) 
                                poses[rope_id][index_]["color"] = color_candidates2[color_id2];
                            else
                                poses[rope_id][index_]["color"] = color_candidates2[0];
                            color_id2 += 1;
                        }
                        else {
                            poses[rope_id][index_]["type"] = cfg.STATICS;
                            // Modify color
                            if (poses[rope_id][index_]["scale"] == cfg.BIG) {
                                if (color_id1 < color_candidates1.Count) 
                                    poses[rope_id][index_]["color"] = color_candidates1[color_id1];
                                else
                                    poses[rope_id][index_]["color"] = color_candidates1[0];
                                color_id1 += 1;
                            }
                            else {
                                if (color_id0 < color_candidates0.Count) 
                                    poses[rope_id][index_]["color"] = color_candidates0[color_id0];
                                else
                                    poses[rope_id][index_]["color"] = color_candidates0[0];
                                color_id0 += 1;
                            }

                        }
                        dynamic_nodes.RemoveAt(index);
                    }
                    dynamics.Add(dynamic_nodes);
                }
            }
            
            // get nodes' relationships
            object_up_down = new();
            AnswerRelations = new();
            List<string> temp0 = new();
            Dictionary<string, int> x_belongs_to = new();
            Dictionary<string, bool> x_is_start = new();
            List<(string, string)> conns = new();
            ropeColor = new();
            // add pulleys
            for (int i = 0; i < poses.Count; i++){
                // set a rope color
                int this_color;
                if (color_idr < color_candidates_r.Count) {
                    this_color = color_candidates_r[color_idr];
                    ropeColor.Add("Cable" + i.ToString(), this_color);
                }
                else{
                    this_color = color_candidates_r[0];
                    ropeColor.Add("Cable" + i.ToString(), this_color);
                }
                color_idr += 1;

                List<string> temp_names = new();
                temp0.Add("");
                string rope_name = GetObjectName(3, null, out _, this_color);
                // inside
                for (int j = 1; j < poses[i].Count - 1; j++) {
                    Dictionary<string, float> pose = poses[i][j];
                    temp_names.Add(rope_name);
                    temp0[i] += rope_name + "|";
                    // Add Dynamic Pulley (with Load)
                    int idx_name = (int)pose["idx"], idy_name = (int)pose["idy"];
                    float x = pose["x"], y = pose["y"], z = pose["z"];
                    string pulley_name = GetObjectName(1, pose, out _);
                    temp_names.Add(pulley_name);
                    temp0[i] += pulley_name + "|";
                    object_up_down.Add(pulley_name, (int)pose["position"] % 2 == 0);
                    // Add Load
                    if (pose["type"] == cfg.DYNAMIC_WITH_LOAD || pose["type"] == cfg.DYNAMIC_WAIT) {
                        string load_name = GetObjectName(2, pose, out _);
                        string conn_name = load_name + " connection with pulley";
                        // set a rope color
                        int rope_color;
                        if (color_idr < color_candidates_r.Count){
                            ropeColor.Add(conn_name, color_candidates_r[color_idr]);
                            rope_color = color_candidates_r[color_idr];
                        }
                        else{
                            ropeColor.Add(conn_name, color_candidates_r[0]);
                            rope_color = color_candidates_r[0];
                        }
                        color_idr += 1;
                        string conn_new_name = GetObjectName(3, null, out _, rope_color);
                        temp_names.Add(conn_new_name);
                        temp_names.Add(load_name);
                        temp0[i] += load_name + "|" + conn_new_name + "|";
                    }
                }
                temp_names.Add(rope_name);
                // two terminals
                Dictionary<string, float> start_pose = poses[i][0], end_pose = poses[i][poses[i].Count - 1];
                for (int j = 0; j < 2; j++){
                    Dictionary<string, float> temp = j == 0 ? start_pose : end_pose;
                    string name = j == 0 ? "Start" : "End";
                    if (temp["type"] == cfg.END_STATIC_CEIL || temp["type"] == cfg.END_STATIC_FLOO || temp["type"] == cfg.END_DYNAMIC_LOAD){
                        if (temp["type"] == cfg.END_DYNAMIC_LOAD) {
                            string load_name = GetObjectName(2, temp, out _);
                            temp_names.Insert(j == 0 ? 0 : temp_names.Count, load_name); // Add Load to State Saving
                            temp0[i] += load_name + "|";
                        }
                        else {
                            string stat_name = GetObjectName(2, temp, out _);
                            temp_names.Insert(j == 0 ? 0 : temp_names.Count, stat_name); // Add Static to State Saving
                            temp0[i] += stat_name + "|";
                            object_up_down.Add(stat_name, temp["type"] == cfg.END_STATIC_CEIL);
                        }
                    }
                    else if (temp["type"] == cfg.END_DYNAMIC_END_LOOP) break;
                    else if(temp["type"] == cfg.END_DYNAMIC_PULY){
                        string part_name = "(" + ((int)temp["target_y"]).ToString() + " " + ((int)temp["target_x"]).ToString() + ")";
                        x_belongs_to.Add(part_name, i);
                        x_is_start.Add(part_name, j == 0);
                    }
                    else{
                        Debug.Log("Pose Type Error: " + temp["type"].ToString());
                    }
                }
                AnswerRelations.Add(temp_names);
            }

            // Add Connections
            for (int j = 0; j < connects.Count; j++) {
                int x1 = connects[j]["x1"], y1 = connects[j]["y1"], x2 = connects[j]["x2"], y2 = connects[j]["y2"];
                conns.Add(("(" + Math.Min(y1, y2).ToString() + " " + x1.ToString() + ")", "(" + Math.Max(y1, y2).ToString() + " " + x2.ToString() + ")"));
                // assign color
                if (color_idr < color_candidates_r.Count) 
                    connRopeColor.Add(color_candidates_r[color_idr]);
                else
                    connRopeColor.Add(color_candidates_r[0]);
                color_idr += 1;
            }

            // merge
            Dictionary<int, int> corres = new();
            int this_label = 0;
            List<(List<string>, int)> corres_conn = new();
            for (int k = 0; k < conns.Count; k++){
                (string a, string b) = conns[k];
                int a_i = -1, b_i = -1;
                List<string> temp_joints = new();
                for (int i = 0; i < AnswerRelations.Count; i++){
                    if (a_i > -1 && b_i > -1) { 
                        break;
                    }
                    if (temp0[i].Contains(a)) {
                        a_i = i;
                    }
                    if (temp0[i].Contains(b)) {
                        b_i = i;
                    }
                }
                if (a_i == -1 || b_i == -1){ Debug.Log("Error! not find a_i / b_i! ");}
                
                int i00=-1, i11=-1;
                for (int i0 = 0; i0 < AnswerRelations[a_i].Count; i0++){
                    if (AnswerRelations[a_i][i0].Contains(a)){
                        i00 = i0;
                        break;
                    }
                }
                for (int i1 = 0; i1 < AnswerRelations[b_i].Count; i1++){
                    if (AnswerRelations[b_i][i1].Contains(b)){
                        i11 = i1;
                        break;
                    }
                }
                bool test_static_a = AnswerRelations[a_i][i00].Contains("Static"), 
                        test_static_b = AnswerRelations[b_i][i11].Contains("Static");
                
                if (test_static_a && test_static_b){
                    corres_conn.Add((new List<string>{AnswerRelations[a_i][i00], 
                                    GetObjectName(3, null, out _, connRopeColor[k]),
                                    AnswerRelations[b_i][i11]}, -1));
                    continue; // no relation
                }
                else if (!test_static_a && test_static_b){
                    corres_conn.Add((new List<string>{AnswerRelations[a_i][i00], 
                                    GetObjectName(3, null, out _, connRopeColor[k]),
                                    AnswerRelations[b_i][i11]}, a_i));
                    continue; // no relation
                }
                else if (test_static_a && !test_static_b){
                    corres_conn.Add((new List<string>{AnswerRelations[a_i][i00], 
                                    GetObjectName(3, null, out _, connRopeColor[k]),
                                    AnswerRelations[b_i][i11]}, b_i));
                    continue; // no relation
                }
                else if (!test_static_a && !test_static_b){
                    corres_conn.Add((new List<string>{AnswerRelations[a_i][i00], 
                                    GetObjectName(3, null, out _, connRopeColor[k]),
                                    AnswerRelations[b_i][i11]}, a_i));
                }

                if (a_i == b_i) {continue;}
                if (corres.ContainsKey(a_i) && corres.ContainsKey(b_i)){
                    int label0 = corres[a_i], label1 = corres[b_i];
                    if (label0 != label1) {
                        corres[b_i] = label0;
                        foreach((int c, int d) in corres){
                            if (d == label1) corres[c] = label0;
                        }
                    }
                }
                else if (corres.ContainsKey(a_i)) {
                    int label0 = corres[a_i];
                    corres.Add(b_i, label0);
                }
                else if (corres.ContainsKey(b_i)) {
                    int label0 = corres[b_i];
                    corres.Add(a_i, label0);
                }
                else {
                    corres.Add(a_i, this_label);
                    corres.Add(b_i, this_label);
                    this_label++;
                }
            }

            foreach((string a, int b_i) in x_belongs_to){
                int a_i = -1;
                for (int i = 0; i < AnswerRelations.Count; i++){
                    if (temp0[i].Contains(a)){
                        a_i = i; break;
                    }
                }
                int ii = -1;
                for (int i = 0; i < AnswerRelations[a_i].Count; i++){
                    if (AnswerRelations[a_i][i].Contains(a)){
                        ii = i; 
                        break;
                    }
                }
                AnswerRelations[b_i].Insert(x_is_start[a] ? 0 : AnswerRelations[b_i].Count, AnswerRelations[a_i][ii]);
                if (AnswerRelations[a_i][ii].Contains("Static")) { 
                    // no relations
                    continue; 
                }
                if (a_i == -1 || b_i == -1){ Debug.Log("Error! x_belongs_to not find a_i / b_i! ");}
                if (a_i == b_i) { continue; }
                if (corres.ContainsKey(a_i) && corres.ContainsKey(b_i)){
                    int label0 = corres[a_i], label1 = corres[b_i];
                    if (label0 != label1) {
                        corres[b_i] = label0;
                        foreach((int c, int d) in corres){
                            if (d == label1) corres[c] = label0;
                        }
                    }
                }
                else if (corres.ContainsKey(a_i)) {
                    int label0 = corres[a_i];
                    corres.Add(b_i, label0);
                }
                else if (corres.ContainsKey(b_i)) {
                    int label0 = corres[b_i];
                    corres.Add(a_i, label0);
                }
                else {
                    corres.Add(a_i, this_label);
                    corres.Add(b_i, this_label);
                    this_label++;
                }
            }
            
            // complete the corresp list
            for (int i=0; i < AnswerRelations.Count; i++) {
                if(!corres.ContainsKey(i)){
                    corres.Add(i, this_label);
                    this_label++;
                }
            }

            // add connection info
            for (int k = 0; k < conns.Count; k++){
                if (corres_conn[k].Item2 > -1)
                    corres_conn[k] = (corres_conn[k].Item1, corres[corres_conn[k].Item2]);
                else {
                    corres_conn[k] = (corres_conn[k].Item1, this_label);
                    this_label++;
                }
            }

            // merge finally
            List<List<string>> AnswerRelations_tp = new();
            for (int i=0; i < this_label; i++) {
                AnswerRelations_tp.Add(new List<string>());
                foreach((int c, int d) in corres){
                    if (d == i) {
                        AnswerRelations_tp[i].AddRange(AnswerRelations[c]);
                    }
                }
            }
            // merge to get rope-level annotation
            List<Dictionary<string, List<string>>>  AnswerRelationsRope_tp = new();
            for (int i=0; i < this_label; i++) {
                AnswerRelationsRope_tp.Add(new Dictionary<string, List<string>>());
                foreach((int c, int d) in corres){
                    if (d == i) {
                        foreach (string item in AnswerRelations[c]) {
                            Debug.Log(item);
                        }
                        Debug.Log(" " + AnswerRelations[c].Count);
                        var ropeStrings = AnswerRelations[c].Where(s => s.Contains("Rope"));
                        var groupedRopeStrings = ropeStrings.GroupBy(s => s).Select(g => new { Value = g.Key, Count = g.Count() });
                        var orderedRope = groupedRopeStrings.OrderByDescending(g => g.Count);
                        var mostRepeatedRopeString = orderedRope.FirstOrDefault();
                        string rope = mostRepeatedRopeString.Value;
                        List<string> answer = new();
                        foreach (string item in AnswerRelations[c]) {
                            answer.Add(new string(item));
                        }
                        foreach(var rp in orderedRope){
                            if (rp.Value == rope) continue;
                            for (int i1 = 0; i1 < AnswerRelations[c].Count; i1++){
                                if (AnswerRelations[c][i1] == rp.Value){
                                    AnswerRelationsRope_tp[i].Add(rp.Value, new List<string>{
                                        AnswerRelations[c][i1-1],
                                        AnswerRelations[c][i1], 
                                        AnswerRelations[c][i1+1]
                                    });
                                    answer.Remove(AnswerRelations[c][i1]);
                                    answer.Remove(AnswerRelations[c][i1 + 1]);
                                    break;
                                }
                            }
                        }
                        AnswerRelationsRope_tp[i].Add(rope, answer);
                    }
                }
                foreach((List<string> s, int d) in corres_conn){
                    if (d == i) {
                        AnswerRelationsRope_tp[i].Add(s[1], s);
                    }
                }
            }
            AnswerRelations = AnswerRelations_tp;
            AnswerRelationsInRope = AnswerRelationsRope_tp;
            // statistic one freedom object
            GroupIdfor1DoF = new();
            GroupIdfor2DoF = new();
            for ( int i = 0; i < AnswerRelations.Count; i++ ){
                int num_dyn = 0;
                foreach ( string j in AnswerRelations[i] ) { 
                    if ( j.Contains("Cube") || j.Contains("Sphere") ) { 
                        num_dyn += 1;
                    }
                }
                if ( num_dyn == 2 ){
                    GroupIdfor2DoF.Add(i);
                }
                else if ( num_dyn == 1 ){
                    GroupIdfor1DoF.Add(i);
                }
            }
            // 
            Debug.Log("finished sequence scheme");
        }
        else if (mode == 1){ // 3D pulley
            // pass
        }
    }
    
    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        if (!validity) { 
            return temp; 
        }                
        if (cfg.ANNO_MODE == cfg.RANDOM_MASS_MOTION){
            temp.Add("mode", "FACTUAL_PREDICTIVE_set_random_mass");
            temp.Add("FrameIndexToSetRandomMass", (int)(cfg.random_mass_motion_start_time/2 - 2));
            temp.Add("relatedGroups", AnswerRelations);
            temp.Add("relatedGroupsInRope", AnswerRelationsInRope);
            AnswerRotation(); 
            temp.Add("ResultRotation", RotationAnswers);
            AnswerMotion(); 
            temp.Add("ResultMotion", MotionAnswers);
            temp.Add("ResultTension", Tension);
            temp.Add("ResultTensionAvg", TensionAvg);
            foreach (GameObject rope in Ropes){
                string rope_name = rope.name;
                if (toPredictAnswerEvent[rope_name] == "null"){
                    toPredictAnswerEvent[rope_name] = "None";
                    toPredictAnswerPulleyName[rope_name] = "None";
                }
            }
            temp.Add("PredictiveEventType", toPredictAnswerEvent);
            temp.Add("PredictiveEventPulleyName", toPredictAnswerPulleyName);
            
            // meta data saving
            Dictionary<string, object> temp_meta = new();
            temp_meta.Add("metaSampling", poses);
            temp_meta.Add("connection", connects);
            temp_meta.Add("division", divisions);
            temp_meta.Add("name2idsInPose", name2idsInPose);
            Dictionary<string, List<float>> name2positionLs = new();
            foreach (var item in name2position){
                name2positionLs.Add(item.Key, new List<float>{item.Value.x, item.Value.y});
            }
            temp_meta.Add("name2position", name2positionLs);
            if (cfg.keep_2_dyn_nodes_per_cable)
                temp_meta.Add("dynamicObjectIndex", dynamics);
            temp.Add("metaSamplingData", temp_meta);
        }
        else if (cfg.ANNO_MODE == cfg.BALANCE_TENSION){//pass
        }
        else if (cfg.ANNO_MODE == cfg.MASS_VARY_PREDICTIVE){
            Dictionary<string, object> temp_temp = new Dictionary<string, object>();
            temp_temp.Add("mode", "COUNTERFACTUAL_change_one_object_mass");
            temp_temp.Add("TargetObj_Name", QuestionMassChange_ColorMassBA.Item1);
            temp_temp.Add("TargetObj_Mass_Before_After", (QuestionMassChange_ColorMassBA.Item2, QuestionMassChange_ColorMassBA.Item3));
            AnswerRotation(); 
            temp_temp.Add("ResultRotation", RotationAnswers);
            AnswerMotion(); 
            temp_temp.Add("ResultMotion", MotionAnswers);
            // reverse answers
            Dictionary<string, object> temp_temp1 = new Dictionary<string, object>();
            temp_temp1.Add("mode", "COUNTERFACTUAL_change_one_object_mass");
            temp_temp1.Add("TargetObj_Name", QuestionMassChange_ColorMassBA.Item1);
            temp_temp1.Add("TargetObj_Mass_Before_After", (QuestionMassChange_ColorMassBA.Item2, 2*QuestionMassChange_ColorMassBA.Item2 - QuestionMassChange_ColorMassBA.Item3));
            Dictionary<string, int> RotationAnswers1 = new Dictionary<string, int>();
            foreach (var item in RotationAnswers){
                RotationAnswers1.Add(item.Key, -item.Value);
            }
            Dictionary<string, int> MotionAnswers1 = new Dictionary<string, int>();
            foreach (var item in MotionAnswers){
                MotionAnswers1.Add(item.Key, -item.Value);
            }
            temp_temp1.Add("ResultRotation", RotationAnswers1);
            temp_temp1.Add("ResultMotion", MotionAnswers1);
            temp.Add("0", temp_temp);
            temp.Add("1", temp_temp1);
        }
        else if (cfg.ANNO_MODE == cfg.MOVE_STH){
            Dictionary<string, object> temp_temp = new Dictionary<string, object>();
            temp_temp.Add("mode", "COUNTERFACTUAL_pull_object_up_or_down");
            temp_temp.Add("TargetMovingAgent", QuestionMovingAgent);
            AnswerRotation(); 
            temp_temp.Add("ResultRotation", RotationAnswers);
            AnswerMotion();
            temp_temp.Add("ResultMotion", MotionAnswers);
            // reverse answers
            Dictionary<string, object> temp_temp1 = new Dictionary<string, object>();
            temp_temp1.Add("mode", "COUNTERFACTUAL_pull_object_up_or_down");
            Dictionary<string, string> QuestionMovingAgent1 = new ();
            foreach (var item in QuestionMovingAgent){
                QuestionMovingAgent1.Add(item.Key, item.Value);
            }
            QuestionMovingAgent1["direction"] = QuestionMovingAgent1["direction"] == "up"? "down":"up";
            temp_temp1.Add("TargetMovingAgent", QuestionMovingAgent1);
            Dictionary<string, int> RotationAnswers1 = new Dictionary<string, int>();
            foreach (var item in RotationAnswers){
                RotationAnswers1.Add(item.Key, -item.Value);
            }
            Dictionary<string, int> MotionAnswers1 = new Dictionary<string, int>();
            foreach (var item in MotionAnswers){
                MotionAnswers1.Add(item.Key, -item.Value);
            }
            temp_temp1.Add("ResultRotation", RotationAnswers1);
            temp_temp1.Add("ResultMotion", MotionAnswers1);
            temp.Add("0", temp_temp);
            temp.Add("1", temp_temp1);
        }
        return temp;
    }

    public void SwitchCapture(bool enable){
        PerceptionCamera pc = _maincamera.GetComponent<PerceptionCamera>();
        pc.captureRgbImages = enable;
        foreach(CameraLabeler cl in pc.labelers){
            cl.enabled = enable;
        }
    }

    public void ModeCF (){
        // switch generation mode's params
        var rdmzr = (_Randomizers["MassRandomizer"] as MassRandomizer);
        rdmzr.categories_mode = true;
        if (cfg.ANNO_MODE == cfg.RANDOM_MASS_MOTION){
            answer_refered_start_frame = cfg.random_mass_motion_start_time;
            _scenario.framesPerIteration = cfg.FRAMES_RANDOM_MASS_MOTION + cfg.ADDITIONAL_TIME4PREDICTIVE;
        }
        else if (cfg.ANNO_MODE == cfg.MOVE_STH){
            answer_refered_start_frame = cfg.move_sth_start_time;
            _scenario.framesPerIteration = cfg.FRAMES_MOVE_STH;
        }
        else if (cfg.ANNO_MODE == cfg.MASS_VARY_PREDICTIVE){
            answer_refered_start_frame = cfg.mass_change_start_time;
            _scenario.framesPerIteration = cfg.FRAMES_MASS_VARY_PREDICTIVE;
        }
        rdmzr.mass_.SetOptions(new List<float>(){cfg.mass_const});
    }

    public override void BuildScheme()
    {
        // voxel output settings
        voxel_absolute_size = cfg.voxel_absolute_size;
        realtime_voxelize  = true;

        // video saving logic settings here
        start_image_id = 1;
        end_image_id = 500;

        cfg.ANNO_MODE = cfg.RANDOM_MASS_MOTION;
        cFMode = true;
        _total_valid_iter = cfg.ValidIterNum;
        _framerate = cfg.frameRate;
        UnityEngine.Random.InitState(cfg.randomSeed);
        string idconfig_path = "PerceptionConfigs/PulleyGroupIdLabelConfig";
        string ssconfig_path = "PerceptionConfigs/PulleyGroupSemanticSegmentationLabelConfig";
        if (Is_Both_Label_Configs_Exist_and_Not_Empty(idconfig_path, ssconfig_path))
        {
            // Init Neccessary Caches
            _scenario = GameObject.Find("Simulation").GetComponent(typeof(UnityEngine.Perception.Randomization.Scenarios.MainDataGeneration)) as UnityEngine.Perception.Randomization.Scenarios.MainDataGeneration;
            _maincamera = GameObject.Find("Main Camera");

            // set simulation settings
            _scenario.framesPerIteration = cfg.framesPerIteration;
            _scenario.constants.randomSeed = cfg.randomSeed;
            _scenario.constants.iterationCount = cfg.iterationCount;
            _scenario.constants.startIteration = cfg.startIteration;

            // Init Randomizer
            PulleyGroupManager r1 = this;
            CameraRandomizer r4 = new CameraRandomizer();
            MassRandomizer r2 = new MassRandomizer();


            // Change the Sample Parameters
            r4.lookat = new UnityEngine.Vector3(cfg.look_at_x, cfg.look_at_y, cfg.look_at_z);
            r4.cam_angle  = new() { value = new NormalSampler(cfg.cam_lr_min, cfg.cam_lr_max, cfg.cam_lr_mean, cfg.cam_lr_std) };
            r4.cam_angle2  = new() { value = new NormalSampler(cfg.cam_ud_min, cfg.cam_ud_max, cfg.cam_ud_mean, cfg.cam_ud_std) };
            r4.cam_radius  = new() { value = new NormalSampler(cfg.cam_dist_min, cfg.cam_dist_max, cfg.cam_dist_mean, cfg.cam_dist_std) };
            r4.cam_fov  = new() { value = new UniformSampler(cfg.cam_fov_min, cfg.cam_fov_max) };
            r4.mirror = true;

            // Add Randomizer
            AddRandomizerAtLast("MainRandomizer", r1);
            AddRandomizerAtLast("MassRandomizer", r2);
            AddRandomizerAtLast("CameraRandomizer", r4);

            // Add Tag to Camera (others added when iter start)
            AddRandTagToObject(_maincamera, typeof(PulleyGroupRandomizerTag));
            AddRandTagToObject(_maincamera, typeof(CameraRandomizerTag));

            // simulation delta time
            _maincamera.GetComponent<PerceptionCamera>().simulationDeltaTime = cfg.simDeltaTime;
            _maincamera.GetComponent<PerceptionCamera>().framesBetweenCaptures = cfg.framesBetweenCaptures;

            // Add Labeler and Set Config File
            if (cfg.output_AnnoImage)
                {AddPcptCamWithLabeler(typeof(BoundingBox2DLabeler), idconfig_path);
                AddPcptCamWithLabeler(typeof(InstanceSegmentationLabeler), idconfig_path);}

            if (!cfg.annotation_Visualization)
                _maincamera.GetComponent<PerceptionCamera>().showVisualizations = false;
        }    
    }

    protected void SetObject_P3DMColor(GameObject rope, int color){
        rope.GetComponent<MeshRenderer>().material.SetColor("_BASE_COLOR", SoftColor.color_rgb_dict[color]);
    }

    // inside pulley: 1, load/terminal: 2, rope: 3, shaft: 4
    protected string GetObjectName(int type, Dictionary<string, float> pose, out UnityEngine.Vector2 position, int colorId=-1, string pulleyName = null){
        string name;
        if (type == 1){
            // pulley
            if (pose["type"] == cfg.STATICS || pose["type"] == cfg.DYNAMIC_WITH_LOAD || pose["type"] == cfg.DYNAMIC_WAIT || pose["type"] == cfg.DYNAMIC_WITH_DYN || pose["type"] == cfg.DYNAMIC_WITH_END) {
                string state_nm = pose["type"] == cfg.STATICS ? "Static" : "Dynamic";
                name = pose["scale"] == cfg.BIG ? "Hollow " + state_nm + " Pulley" : "Solid " + state_nm + " Pulley";
                string color_name = SoftColor.color_name_dict[(int)pose["color"]];
                name  = color_name + " " + name;
                int idx_name = (int)pose["idx"], idy_name = (int)pose["idy"];
                string add_name = "(" + idy_name.ToString() + " " + idx_name.ToString() + ")";
                name = name + " " + add_name;
                position = new UnityEngine.Vector2(pose["x"], pose["y"]);
                return name;
            }
            else {
                throw new System.ArgumentException("Error type: " + pose["type"].ToString());
            }
        }
        
        else if (type == 2){
            if (pose["type"] == cfg.DYNAMIC_WITH_LOAD || pose["type"] == cfg.DYNAMIC_WAIT) {
                // load under a dyn pulley
                string shape = pose["loadShape"] == 1 ? "Cube" : "Sphere";
                int loadColor = (int)pose["loadColor"];
                name = SoftColor.color_name_dict[loadColor] + " " + shape;
                position = new UnityEngine.Vector2(pose["x"], pose["y"] - 0.5f * cfg.y_step);
                return name;
            }
            else if (pose["type"] == cfg.END_STATIC_CEIL || pose["type"] == cfg.END_STATIC_FLOO || pose["type"] == cfg.END_DYNAMIC_LOAD){
                // tetrminal point
                if (pose["type"] == cfg.END_DYNAMIC_LOAD){
                    int loadColor = (int)pose["color"];
                    string color_name = SoftColor.color_name_dict[loadColor];
                    string shape = pose["loadShape"] == 1 ? "Cube" : "Sphere";
                    name = color_name + " " + shape;
                }
                else {
                    name = SoftColor.color_name_dict[(int)pose["color"]] + " Fixed Point";
                }
                position = new UnityEngine.Vector2(pose["x"], pose["y"]);
                return name;
            }
            else if (pose["type"] == cfg.END_DYNAMIC_END_LOOP || pose["type"] == cfg.END_DYNAMIC_PULY) {
                position = new UnityEngine.Vector2(0, 0);
                return null; // pass
            }
            else{
                throw new System.ArgumentException("Error type: " + pose["type"].ToString());
            }
        }
        else if (type == 3){
            // rope
            name = SoftColor.color_name_dict[colorId] + " Rope";
            position = new UnityEngine.Vector2(0, 0);
            return name;
        }
        else if (type == 4){
            // shaft
            name = "Shaft of " + pulleyName;
            position = new UnityEngine.Vector2(0, 0);
            return name;
        }
        else{
            throw new System.ArgumentException("Error type: " + type.ToString());
        }
        
    }

    // You Should use AddObject/AddObjectFromPrefab/AddRandTagToObject/AddStateSavingObject funcs
    protected void RearrangeObjects(){
        name2idsInPose = new();
        name2position = new();
        for (int i = 0; i < poses.Count; i++){
            for (int j = 0; j < poses[i].Count; j++) {
                Dictionary<string, float> pose = poses[i][j];
                string name = GetObjectName((j == 0 || j == poses[i].Count-1) ? 2 : 1, pose, out UnityEngine.Vector2 position);
                if (name == null) continue;
                else if (!name2idsInPose.ContainsKey(name)){
                    name2idsInPose.Add(name, new List<int>(){i, j});
                    name2position.Add(name, position);
                    if (pose["type"] == cfg.DYNAMIC_WITH_LOAD || pose["type"] == cfg.DYNAMIC_WAIT) { // with load under pulley
                        string name_load = GetObjectName(2, pose, out UnityEngine.Vector2 position_load);
                        name2idsInPose.Add(name_load, new List<int>(){i, j});
                        name2position.Add(name_load, position_load);
                    }
                }
            }
        }
        // each group: get the closest pairs with other group
        // get group unique object list
        List<List<string>> group_unique_objects = new();
        foreach (List<string> group in AnswerRelations){
            List<string> group_unique_objects0 = new();
            foreach (string name in group){
                if ((!group_unique_objects0.Contains(name)) && (!name.Contains("Rope"))) group_unique_objects0.Add(name);
            }
            group_unique_objects.Add(group_unique_objects0);
        }
        Debug.Log("group_unique_objects.Count: " + group_unique_objects.Count.ToString());
        // combine 
        Dictionary<int, int> group_in_gen = new();
        int this_id = 0;
        for (int i=0; i<group_unique_objects.Count; i++){
            List<string> group1 = group_unique_objects[i];
            Debug.Log("group_in_gen.Count: " + group1.Count.ToString() );
            group_in_gen.Add(i, this_id);
            this_id += 1;
        }
        for (int i=0; i<group_unique_objects.Count; i++){
            for (int j=i+1; j<group_unique_objects.Count; j++){
                List<string> group1 = group_unique_objects[i], group2 = group_unique_objects[j];
                Debug.Log("group_in_gen.Count: " + group1.Count.ToString() + ", " + group2.Count.ToString());
                foreach (string x1 in group1){
                    foreach(string x2 in group2){
                        if (x1 == x2) {
                            if (group_in_gen.ContainsKey(i) && !group_in_gen.ContainsKey(j)) group_in_gen.Add(j, group_in_gen[i]);
                            else if (!group_in_gen.ContainsKey(i) && group_in_gen.ContainsKey(j)) group_in_gen.Add(i, group_in_gen[j]);
                            else if (!group_in_gen.ContainsKey(i) && !group_in_gen.ContainsKey(j)) {
                                group_in_gen.Add(i, this_id);
                                group_in_gen.Add(j, this_id);
                                this_id += 1;
                            }
                            else {
                                int id1 = group_in_gen[i], id2 = group_in_gen[j];
                                foreach (int k in group_in_gen.Keys){
                                    if (group_in_gen[k] == id2) group_in_gen[k] = id1;
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("group_in_gen.Count: " + group_in_gen.Count.ToString());
        foreach (int id in group_in_gen.Keys){
            Debug.Log("group_in_gen: " + id +  " "+ group_in_gen[id].ToString());
        }
        // combine the same id groups in group_unique_objects
        Dictionary<int, List<string>> group_unique_objects_new = new();
        foreach (int id in group_in_gen.Values){
            if (!group_unique_objects_new.ContainsKey(id)) group_unique_objects_new.Add(id, new List<string>());
        }
        for (int i=0; i<group_unique_objects.Count; i++){
            int id = group_in_gen[i];
            foreach (string name in group_unique_objects[i]){
                if (!group_unique_objects_new[id].Contains(name)) group_unique_objects_new[id].Add(name);
            }
        }
        // get the closest pairs
        if (group_unique_objects_new.Count == 1) {return;}
        bool random_layout = false;
        foreach (int id1 in group_unique_objects_new.Keys){
            foreach (int id2 in group_unique_objects_new.Keys){
                if (id1 == id2) continue;
                List<string> group1 = group_unique_objects_new[id1], group2 = group_unique_objects_new[id2];
                foreach (string x1 in group1){
                    foreach(string x2 in group2){
                        if (x1 == x2) continue;
                        UnityEngine.Vector2 pos1 = name2position[x1], pos2 = name2position[x2];
                        float dist = (pos1 - pos2).magnitude;
                        if (dist < cfg.min_dist_between_objects){
                            random_layout = true;
                            break;
                        }
                    }
                    if (random_layout) break;
                }
                if (random_layout) break;
            }
            if (random_layout) break;
        }
        random_layout = rand_01.Sample() < cfg.random_layout_prob ? true : random_layout;
        // if not too close or not selected to be random layout, then return
        if (!random_layout) return;
        // get height, width, center position of each group
        Dictionary<int, float> group_height = new();
        Dictionary<int, float> group_width = new();
        Dictionary<int, float> group_center_x = new(), target_x = new();
        Dictionary<int, float> group_center_y = new(), target_y = new();
        foreach (int id in group_unique_objects_new.Keys){
            float min_x = 100000, max_x = -100000, min_y = 100000, max_y = -100000;
            foreach (string name in group_unique_objects_new[id]){
                UnityEngine.Vector2 pos = name2position[name];
                min_x = Mathf.Min(min_x, pos.x);
                max_x = Mathf.Max(max_x, pos.x);
                min_y = Mathf.Min(min_y, pos.y);
                max_y = Mathf.Max(max_y, pos.y);
            }
            group_height.Add(id, max_y - min_y);
            group_width.Add(id, max_x - min_x);
            group_center_x.Add(id, (min_x + max_x) / 2);
            group_center_y.Add(id, (min_y + max_y) / 2);
        }
        // arrange x
        // random number list
        List<int> random_list = new();
        for (int i=0; i<group_unique_objects_new.Count; i++) random_list.Add(i);
        random_list = random_list.OrderBy(x => rand_01.Sample()).ToList();
        // arrange x
        float base_x = 0;
        for( int i=0; i<random_list.Count; i++){
            int id = random_list[i];
            float width = group_width[id];
            target_x.Add(id, base_x + width / 2);
            base_x += width + cfg.x_step_for_rearranged_group;
        }
        // arrange y
        // get max y and min y
        float max_y_total = -100000, min_y_total = 100000;
        foreach (int id in group_unique_objects_new.Keys){
            max_y_total = Mathf.Max(max_y_total, group_center_y[id] + group_height[id] / 2);
            min_y_total = Mathf.Min(min_y_total, group_center_y[id] - group_height[id] / 2);
        }
        float height_total = max_y_total - min_y_total;
        foreach (int id in group_unique_objects_new.Keys){
            float half_height = group_height[id] / 2;
            float base_y_up = 0 - half_height, base_y_down = -height_total + half_height;
            // sample a point between down and up
            float base_y = rand_01.Sample() * (base_y_up - base_y_down) + base_y_down; 
            target_y.Add(id, base_y);
        }
        GetBarycentric(group_width, group_height, target_x, target_y, out float group_barycentric_x, out float group_barycentric_y);
        GetBarycentric(group_width, group_height, group_center_x, group_center_y, out float group_barycentric_x0, out float group_barycentric_y0);
        float delta_x = - group_barycentric_x + group_barycentric_x0, delta_y = - group_barycentric_y + group_barycentric_y0;
        // give each item new position to get name2position_2
        Dictionary<string, UnityEngine.Vector2> name2position_2 = new();
        foreach (int id in group_unique_objects_new.Keys){
            var x1 = target_x[id] - group_center_x[id] + delta_x;
            var y1 = target_y[id] - group_center_y[id] + delta_y;
            foreach (string name in group_unique_objects_new[id]){
                UnityEngine.Vector2 pos = name2position[name];
                UnityEngine.Vector2 pos_new = new UnityEngine.Vector2(pos.x + x1, pos.y + y1);
                name2position_2.Add(name, pos_new);
            }
        }
        // edit the position in pose data structure
        name2position = name2position_2;
        for (int i = 0; i < poses.Count; i++){
            for (int j = 0; j < poses[i].Count; j++) {
                Dictionary<string, float> pose = poses[i][j];
                string name = GetObjectName((j == 0 || j == poses[i].Count-1) ? 2 : 1, pose, out UnityEngine.Vector2 position);
                if (name == null) continue;
                else if (name2position.ContainsKey(name)){
                    pose["x"] = name2position[name].x;
                    pose["y"] = name2position[name].y;
                }
            }
        }
    }
    
    void GetBarycentric(Dictionary<int, float> group_width, 
                        Dictionary<int, float> group_height, 
                        Dictionary<int, float> group_center_x, 
                        Dictionary<int, float> group_center_y, 
                        out float group_barycentric_x, 
                        out float group_barycentric_y){
        // get barycentric
        group_barycentric_x = new();
        group_barycentric_y = new();
        float min_x = 100000, max_x = -100000, min_y = 100000, max_y = -100000;
        foreach (int id in group_width.Keys){
            min_x = Mathf.Min(min_x, group_center_x[id] - group_width[id] / 2);
            max_x = Mathf.Max(max_x, group_center_x[id] + group_width[id] / 2);
            min_y = Mathf.Min(min_y, group_center_y[id] - group_height[id] / 2);
            max_y = Mathf.Max(max_y, group_center_y[id] + group_height[id] / 2);
        }
        group_barycentric_x = (min_x + max_x) / 2;
        group_barycentric_y = (min_y + max_y) / 2;
    }

    // You Should use AddObject/AddObjectFromPrefab/AddRandTagToObject/AddStateSavingObject funcs
    protected override void BuildIterScene(){
        // Build Scene
        RopeRecorder.onCapture = false;
        Chains = new();
        Ropes = new();
        Cubes = new();
        GameObject env = GameObject.Find("TestEnvironment");
        GameObject.DestroyImmediate(env);
        RenderSettings.ambientIntensity = cfg.ambientIntensity;
        _maincamera.transform.position = new UnityEngine.Vector3(0, 0, 0);
        // Build Pulley Chains
        List<List<GameObject>> Chains0 = new();
        List<GameObject> Ropes0 = new();

        // add pulleys
        for (int i = 0; i < poses.Count; i++){
            // add cable
            int color_r = ropeColor["Cable" + i.ToString()];
            string rope_name = GetObjectName(3, null, out _, color_r);
            GameObject cable = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo Rope", rope_name, new UnityEngine.Vector3(), UnityEngine.Quaternion.identity);
            cable.AddComponent<RopeRecorder>();
            AddStateWriter(rope_name, cable, is_softbody:true); // Add rope to State Saving
            SetObject_P3DMColor(cable, color_r);
            Cable cb = cable.GetComponent(typeof(Cable)) as Cable;
            // add inside points/pulleys
            List<GameObject> Chain = new List<GameObject>();
            for (int j = 1; j < poses[i].Count - 1; j++) {
                Dictionary<string, float> pose = poses[i][j];
                // Add Dynamic Pulley (with Load)
                string scale_name;
                int color = -1;
                if (pose["type"] == cfg.STATICS) {
                    scale_name = pose["scale"] == cfg.BIG ? "Filo Large Pulley Static" : "Filo Little Pulley Static";
                }
                else if (pose["type"] == cfg.DYNAMIC_WITH_LOAD || pose["type"] == cfg.DYNAMIC_WAIT || pose["type"] == cfg.DYNAMIC_WITH_DYN || pose["type"] == cfg.DYNAMIC_WITH_END) {
                    scale_name = pose["scale"] == cfg.BIG ? "Filo Large Pulley Dynamic" : "Filo Little Pulley Dynamic";
                }
                else{
                    scale_name = "";
                }
                color = (int)pose["color"];
                string color_name = SoftColor.color_name_dict[(int)pose["color"]];
                int idx_name = (int)pose["idx"], idy_name = (int)pose["idy"];
                float x = pose["x"], y = pose["y"], z = pose["z"];
                string pulley_name = GetObjectName(1, pose, out _);
                GameObject pulley0 = AddObjectFromPrefab("Prefabs/PulleyGroup/" + scale_name, pulley_name, new UnityEngine.Vector3(x, y, z), UnityEngine.Quaternion.identity);
                pulley0.transform.localScale = cfg.pulley_scale * pulley0.transform.localScale;
                AddStateWriter(pulley_name, pulley0); // Add Pulley to State Saving
                pulley0.AddComponent<CollisionStopIter>()._manager = this;
                if (pose["type"] == cfg.STATICS){
                    // find the pulley0's child called "supporter"
                    GameObject supporter = null;
                    for (int ii=0; ii<pulley0.transform.childCount; ii++){
                        var child = pulley0.transform.GetChild(ii);
                        if (child.name == "Supporter"){
                            supporter = child.gameObject;
                            break;
                        }
                    }
                    string supporterName = GetObjectName(4, pose, out _, pulleyName: pulley_name);
                    supporter.name = supporterName;
                    AddStateWriter(supporterName, supporter, is_static:true); // Add Pulley to State Saving
                    
                }
                else {
                }
                if (color != -1) {
                    (pulley0.GetComponent<MeshRenderer>()).material.SetColor("_BASE_COLOR", SoftColor.color_rgb_dict[color]);
                    for (int ii = 0 ; ii < pulley0.transform.childCount ; ii++){
                        var child = pulley0.transform.GetChild(ii);
                        if (child.name.Contains("Cylinder"))
                            (child.GetComponent<MeshRenderer>()).material.SetColor("_BASE_COLOR", SoftColor.color_rgb_dict[color]);
                    }
                }
                Chain.Add(pulley0);
                Cable.Link element = new();
                element.type = Cable.Link.LinkType.Rolling;
                element.body = pulley0.GetComponent(typeof(CableDisc)) as CableDisc;
                element = FixOrientation(cfg.fix_orientation, element, idy_name, (int)pose["position"], j, poses[i]);
                cb.links.Add(element);
                // Add Load
                if (pose["type"] == cfg.DYNAMIC_WITH_LOAD || pose["type"] == cfg.DYNAMIC_WAIT) {
                    string shape = pose["loadShape"] == 1 ? "Cube" : "Sphere";
                    int loadColor = (int)pose["loadColor"];
                    string load_name = GetObjectName(2, pose, out _);
                    string conn_name = load_name + " connection with pulley";
                    int rope_color = ropeColor[conn_name];
                    string conn_new_name = GetObjectName(3, null, out _, rope_color);
                    GameObject load = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo " + shape, load_name, new UnityEngine.Vector3(x, y - 0.5f * cfg.y_step, z), UnityEngine.Quaternion.identity);
                    Cubes.Add((load, loadColor));
                    (load.AddComponent(typeof(CollisionStopIter)) as CollisionStopIter)._manager = this;
                    GameObject conn = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo Rope", conn_new_name, new UnityEngine.Vector3(), UnityEngine.Quaternion.identity);
                    conn.AddComponent<RopeRecorder>();
                    AddStateWriter(conn_new_name, conn, is_softbody:true); // Add Pulley to State Saving

                    SetObject_P3DMColor(conn, rope_color);
                    if (loadColor != -1) {
                        SetObject_P3DMColor(load, loadColor);// it's load color
                    }
                    Cable conn_cable = conn.GetComponent(typeof(Cable)) as Cable;
                    conn_cable.dynamicSplitMerge = false;
                    Cable.Link link = new();
                    link.type = Cable.Link.LinkType.Attachment;
                    link.body = load.GetComponent(typeof(CablePoint)) as CablePoint;
                    link.outAnchor = new UnityEngine.Vector3(0, cfg.insert_depth, 0);
                    conn_cable.links.Add(link);
                    Cable.Link link1 = new();
                    link1.type = Cable.Link.LinkType.Attachment;
                    link1.body = pulley0.GetComponent(typeof(CableDisc)) as CableDisc;
                    conn_cable.links.Add(link1);
                    Ropes.Add(conn); // Add to Ropes Set
                    AddRandTagToObject(load, typeof(MassRandomizerTag)); // Add Load to Randomizer
                    if (cfg.enable_pulley_load_mass_doubled)
                        (load.GetComponent(typeof(MassRandomizerTag)) as MassRandomizerTag).mass_multifier = cfg.pulley_load_multifier;
                    AddStateWriter(load_name, load); // Add Load to State Saving
                }
            }
            Chains0.Add(Chain);
            Ropes0.Add(cable);
        }
        // add terminal point
        for (int i = 0; i < poses.Count; i++) {
            GameObject cable0 = Ropes0[i];
            Cable cb0 = cable0.GetComponent(typeof(Cable)) as Cable;
            // add inside points/pulleys
            List<GameObject> Chain0 = Chains0[i];
            GameObject term;
            Dictionary<string, float> start_pose = poses[i][0], end_pose = poses[i][poses[i].Count - 1];
            for (int j = 0; j < 2; j++){
                int idex = j * (cb0.links.Count);
                Dictionary<string, float> temp = j == 0 ? start_pose : end_pose;
                string name = j == 0 ? "Start" : "End";
                if (temp["type"] == cfg.END_STATIC_CEIL || temp["type"] == cfg.END_STATIC_FLOO || temp["type"] == cfg.END_DYNAMIC_LOAD){
                    // Static
                    float x = temp["x"], y = temp["y"], z = temp["z"];
                    if (temp["type"] == cfg.END_DYNAMIC_LOAD){
                        int loadColor = (int)temp["color"];
                        string color_name = SoftColor.color_name_dict[loadColor];
                        string shape = temp["loadShape"] == 1 ? "Cube" : "Sphere";
                        string load_name = GetObjectName(2, temp, out _);
                        term = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo " + shape, load_name, new UnityEngine.Vector3(x, y, z), UnityEngine.Quaternion.identity);
                        Cubes.Add((term, loadColor));
                        (term.AddComponent(typeof(CollisionStopIter)) as CollisionStopIter)._manager = this;
                        SetObject_P3DMColor(term, loadColor); // it's load color
                        AddRandTagToObject(term, typeof(MassRandomizerTag));  // Add Load to Randomizer
                        AddStateWriter(load_name, term); // Add Load to State Saving
                    }
                    else{
                        string stat_name = GetObjectName(2, temp, out _);
                        term = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo Static", stat_name, new UnityEngine.Vector3(x, y, z), UnityEngine.Quaternion.identity); 
                        AddStateWriter(stat_name, term, is_static:true); // Add Load to State Saving
                        // set color
                        (term.GetComponent<MeshRenderer>() as MeshRenderer).material.color = SoftColor.color_rgb_dict[(int)temp["color"]];//(Resources.Load(cfg.pulley_mat[color]) as Material);}
                    }
                    Cable.Link link = new();
                    link.type = Cable.Link.LinkType.Attachment;
                    link.body = term.GetComponent(typeof(CablePoint)) as CablePoint;
                    if (j==0) link.outAnchor = new UnityEngine.Vector3(0, cfg.insert_depth, 0);
                    else link.inAnchor = new UnityEngine.Vector3(0, cfg.insert_depth, 0);
                    cb0.links.Insert(idex, link);
                    Chain0.Insert(idex, term);
                }
                else if (temp["type"] == cfg.END_DYNAMIC_END_LOOP) {
                    float x = (poses[i][1]["x"] + poses[i][poses[i].Count - 2]["x"]) / 2, y = (poses[i][1]["y"] + poses[i][poses[i].Count - 2]["y"]) / 2, z = (poses[i][1]["z"] + poses[i][poses[i].Count - 2]["z"]) / 2;
                    term = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo Connector", name + i.ToString(), new UnityEngine.Vector3(x, y, z), UnityEngine.Quaternion.identity);
                    Cable.Link link = new();
                    link.type = Cable.Link.LinkType.Attachment;
                    link.body = term.GetComponent(typeof(CablePoint)) as CablePoint;
                    link.outAnchor = new UnityEngine.Vector3(0, 0.1f, 0);
                    cb0.links.Insert(0, link);
                    Cable.Link link1 = new();
                    link1.type = Cable.Link.LinkType.Attachment;
                    link1.body = term.GetComponent(typeof(CablePoint)) as CablePoint;
                    link1.inAnchor = new UnityEngine.Vector3(0, -0.1f, 0);
                    cb0.links.Insert(cb0.links.Count, link1);
                    Chain0.Insert(0, term);
                    Chain0.Insert(Chain0.Count, term);
                    break;
                }
                else if(temp["type"] == cfg.END_DYNAMIC_PULY){
                    // Find the generated pulley and connect
                    string pulley_name = "(" + ((int)temp["target_y"]).ToString() + " " + ((int)temp["target_x"]).ToString() + ")";
                    term = null;
                    foreach(CableDisc go in GameObject.FindObjectsOfType(typeof(CableDisc))) {
                        if (go.gameObject.name.Contains(pulley_name)){
                            term = go.gameObject;
                            break;
                        }
                    }
                    Cable.Link link = new();
                    link.type = Cable.Link.LinkType.Attachment;
                    link.body = term.GetComponent(typeof(CableDisc)) as CableDisc;
                    cb0.dynamicSplitMerge = false;
                    cb0.links.Insert(idex, link);
                    Chain0.Insert(idex, term);
                }
                else{
                    Debug.Log("Pose Type Error: " + temp["type"].ToString());
                }
            }
            Chains.Add(Chain0);
            Ropes.Add(cable0);
        }        
        // Add Connections
        for (int j = 0; j < connects.Count; j++){
            int rope_color = connRopeColor[j];
            int x1 = connects[j]["x1"], y1 = connects[j]["y1"], x2 = connects[j]["x2"], y2 = connects[j]["y2"];
            // temp name
            string conn_name = "connection x " + x1.ToString() + " y " + Math.Min(y1, y2).ToString() + " " + Math.Max(y1, y2).ToString();
            GameObject conn = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo Rope", conn_name, new UnityEngine.Vector3(), UnityEngine.Quaternion.identity);
            conn.AddComponent<RopeRecorder>();
            
            Cable conn_cable = conn.GetComponent(typeof(Cable)) as Cable;
            GameObject disc_h = null, disc_l = null;
            string name_h = "(" + Math.Min(y1, y2).ToString() + " " + x1.ToString() + ")";
            string name_l = "(" + Math.Max(y1, y2).ToString() + " " + x2.ToString() + ")";
            foreach(CableDisc go in GameObject.FindObjectsOfType(typeof(CableDisc))) {
                if (go.gameObject.name.Contains(name_h))
                    disc_h = go.gameObject;
                else if (go.gameObject.name.Contains(name_l))
                    disc_l = go.gameObject;
            }
            Cable.Link link = new();
            link.type = Cable.Link.LinkType.Attachment;
            link.body = disc_h.GetComponent(typeof(CableDisc)) as CableDisc;
            conn_cable.links.Add(link);
            Cable.Link linkl = new();
            linkl.type = Cable.Link.LinkType.Attachment;
            linkl.body = disc_l.GetComponent(typeof(CableDisc)) as CableDisc;
            conn_cable.links.Add(linkl);
            conn_cable.dynamicSplitMerge = false;
            // modify rope name and color
            string conn_new_name = GetObjectName(3, null, out _, rope_color);
            conn.name = conn_new_name;
            SetObject_P3DMColor(conn, rope_color);
            AddStateWriter(conn_new_name, conn, is_softbody:true);
            // Add to Rope Set
            Ropes.Add(conn);
        }
        // Add Solver
        GameObject solver = AddObjectFromPrefab("Prefabs/PulleyGroup/Filo Solver", "Solver", new UnityEngine.Vector3(), UnityEngine.Quaternion.identity);
        CableSolver sr = solver.GetComponent(typeof(CableSolver)) as CableSolver;
        List<Cable> cable_ = new List<Cable>();
        foreach (GameObject rope in Ropes){ cable_.Add(rope.GetComponent(typeof(Cable)) as Cable); }
        sr.cables = cable_.ToArray();
        // Add State Saving
    }

    protected void GetGameObjectState(){
        foreach ((string key, GameObject go) in _RigidbodySaveManage){
            List<List<float>> _positions1 = new List<List<float>>();
            _positions1.Add(Vec2List(go.transform.position));
            _positions1.Add(Vec2List(go.transform.rotation.eulerAngles));
            if (go.GetComponent(typeof(CableDisc)) != null) {
                if (go.GetComponent<Rigidbody>().angularVelocity.magnitude > cfg.angularVelocityThreshold){
                    StopThisIterImmediatelyAndInvalidIt(false);
                    Debug.Log("Angular Velocity: " + go.name +", " + go.GetComponent<Rigidbody>().angularVelocity.magnitude);
                }
            }
            if (!_StateSaveCache.ContainsKey(key)) {
                _StateSaveCache.Add(key, new List<List<List<float>>>());
            }
            _StateSaveCache[key].Add(_positions1);
        }
    }

    protected override List<List<float>> GetSoftbodyState(GameObject go){
        var temp = base.GetSoftbodyState(go);
        if (go.GetComponent<Cable>() != null){        
            var x = go.GetComponent<RopeRecorder>().GetTrackedParticles();
            temp.AddRange(x);
        }
        else {
            throw new Exception("Hand Wrote Error: " + go.name);
        }
        return temp;
    }

    protected override (List<List<float>>, List<int>) GetSoftbodyGeometry(GameObject go){
        var temp = base.GetSoftbodyGeometry(go);
        var cable = go.GetComponent<Cable>();
        if (cable != null){      
            var toret = Mesh2StandardFunc(cable.GetComponent<MeshFilter>().sharedMesh, go.transform);
            temp = toret;
        }
        return temp;
    }

    protected override void OnAwake() {
        base.OnAwake();
    }

    protected override void OnScenarioStart() {
        base.OnScenarioStart();
        manualDeleteImages = true;
    }
    
    List<int> GroupIdfor1DoF, GroupIdfor2DoF;
    protected override void OnIterationStart()
    {
        done = false;
        base.OnIterationStart();
        ModeCF();
        Debug.Log("asdasgghfgf, "+ cfg.ANNO_MODE + " " + _this_iter_ft + " " + this_move + " " + this_mass_change + " " + _this_cf_iter);
        _pre_name = cfg.scene_name;
        if (_this_iter_ft){
            float luck = rand_01.Sample();
            do {
                // Sampling
                SampleScheme();
                SequenceScheme();
            } while ( 
                (luck <= 0.7f && GroupIdfor2DoF.Count < 2)|| (luck <= 0.7f && luck <= 0.9f && GroupIdfor2DoF.Count < 1) || (luck >= 0.9f && !(GroupIdfor2DoF.Count > 0 || GroupIdfor1DoF.Count > 1))
            );
            RearrangeObjects();
            _maincamera.SetActive(true);
        }
        // Build Scene
        BuildIterScene();
        _StateSaveCache = new();
    }
    
    Dictionary<string, List<string>> per_rope_attached_items;
    Dictionary<string, string> toPredictAnswerEvent, toPredictAnswerPulleyName;

    protected void RopeDerepeat(){
        if (_this_frame == cfg.check_rope_repeat_element_time){
            // derepeat
            foreach (GameObject rope in Ropes){
                Cable cb = rope.GetComponent(typeof(Cable)) as Cable;
                bool done = false;
                if (cb.links.Count > 3 && !done){
                    string name0 = cb.links[1].body.name;
                    done = true;
                    for (int i = 2; i < cb.links.Count - 1; i++){
                        if (cb.links[i].body.name == name0){
                            cb.links.RemoveAt(i);
                            done = false;
                            break;
                        }
                        else {
                            name0 = cb.links[i].body.name;
                        }
                    }
                }
                cb.Setup();
            }
        }
    }
    
    protected void SetMirrored(){
        CameraRandomizer cr = _Randomizers["CameraRandomizer"] as CameraRandomizer;
        GameObject env;
        if (!cr.mirrored){
            _light = AddObjectFromPrefab(cfg.dirlight_path1, "Directional Light", cfg.light_pos1, cfg.light_rotation1);
            env = AddObjectFromPrefab(cfg.envroom_path, "TestEnvironment", cfg.bg_pos, UnityEngine.Quaternion.Euler(90, 0, 0));
            
        }
        else {
            _light = AddObjectFromPrefab(cfg.dirlight_path, "Directional Light", cfg.light_pos, cfg.light_rotation);
            env = AddObjectFromPrefab(cfg.envroom_path, "TestEnvironment", cfg.bg_pos1, UnityEngine.Quaternion.Euler(-90, 0, 0));
        }
        env.transform.localScale = cfg.room_scale;
        if (rand_01.Sample() < 0.5){
            (env.GetComponent<MeshRenderer>() as MeshRenderer).material = (Resources.Load(cfg.room_mat[0]) as Material);
        }
        else {
            (env.GetComponent<MeshRenderer>() as MeshRenderer).material = (Resources.Load(cfg.room_mat[1]) as Material);
        }
    }
    
    protected void HandleAfterSimulation(){
        // after capturing, while doing predicting
        if (_this_frame == cfg.FRAMES_RANDOM_MASS_MOTION + 1){
            RopeRecorder.onCapture = false;
            _maincamera.SetActive(false);
            per_rope_attached_items = new();
            toPredictAnswerPulleyName = new();
            toPredictAnswerEvent = new();
            foreach (GameObject rope in Ropes){
                Cable cb = rope.GetComponent(typeof(Cable)) as Cable;
                string rope_name = rope.name;
                per_rope_attached_items.Add(rope_name, new());
                toPredictAnswerEvent.Add(rope_name, "None");
                toPredictAnswerPulleyName.Add(rope_name, "None");
                for (int i = 1; i < cb.links.Count-1; i++){
                    string attached_name = cb.links[i].body.name;
                    per_rope_attached_items[rope_name].Add(attached_name);
                }
            }
        }
        else {
            // check each one
            foreach (GameObject rope in Ropes){
                // omit the rope that any event has been predicted
                if (toPredictAnswerEvent[rope.name] != "None") continue;
                Cable cb = rope.GetComponent(typeof(Cable)) as Cable;
                string rope_name = rope.name;
                List<string> attached_items = new(), original_items = per_rope_attached_items[rope_name];
                for (int i = 1; i < cb.links.Count-1; i++){
                    string attached_name = cb.links[i].body.name;
                    attached_items.Add(attached_name);
                }
                bool same = attached_items.Count == original_items.Count;
                if (same){ // check each element to correct the "same" var
                    for (int i = 0; i < attached_items.Count; i++){
                        if (attached_items[i] != original_items[i]){
                            same = false;
                            break;
                        }
                    }
                }
                if (!same){
                    string obj_name = "None", event_name = "None";
                    string obj_name_de = "None", event_name_de = "None";
                    // attach: find object in attached_items but not in original_items
                    for (int i = 0; i < attached_items.Count; i++){
                        if (!original_items.Contains(attached_items[i])){
                            if (obj_name != "None"){
                                obj_name += " / " + attached_items[i];
                            }
                            else {
                                obj_name = attached_items[i];
                            }
                            event_name = "Attach";
                        }
                    }
                    // detach: find object in original_items but not in attached_items
                    for (int i = 0; i < original_items.Count; i++){
                        if (!attached_items.Contains(original_items[i])){
                            if (obj_name_de != "None"){
                                obj_name_de += " / " + original_items[i];
                            }
                            else {
                                obj_name_de = original_items[i];
                            }
                            event_name_de = "Detach";
                        }
                    }

                    if (event_name == "None" && event_name_de == "None"){
                        Debug.Log("Happened repeat issue on : " + rope_name);
                        toPredictAnswerEvent[rope_name] = "null";
                        toPredictAnswerPulleyName[rope_name] = "null";
                    }
                    else if (event_name != "None" && event_name_de == "None"){
                        toPredictAnswerEvent[rope_name] = event_name;
                        toPredictAnswerPulleyName[rope_name] = obj_name;
                    }
                    else if (event_name == "None" && event_name_de != "None"){
                        toPredictAnswerEvent[rope_name] = event_name_de;
                        toPredictAnswerPulleyName[rope_name] = obj_name_de;
                    }
                    else { // both de/attach event happens, use null temporarily and finally it will turn to be "None" 
                        toPredictAnswerEvent[rope_name] = "null";
                        toPredictAnswerPulleyName[rope_name] = "null";
                    }
                }
            }
        }
    }

    protected override void OnUpdate() {
        Debug.Log("frame:" + _this_frame);
        base.OnUpdate();
        GetGameObjectState();
        RopeDerepeat();
        if (cfg.ANNO_MODE == cfg.RANDOM_MASS_MOTION){
            SetMass();
            if (_this_frame == 1){
                _maincamera.SetActive(false);
            }
            else if (_this_frame == 2){ // decorate lights according to the camera mirror state
                SetMirrored();
            }
            else if (_this_frame == cfg.random_mass_motion_start_time){
                RopeRecorder.onCapture = true;
                _maincamera.SetActive(true);
                SetCamera();
            }
            else if (_this_frame > cfg.FRAMES_RANDOM_MASS_MOTION){
                HandleAfterSimulation();
            }
        }
        else if (cfg.ANNO_MODE == cfg.MOVE_STH){
            MovingAgent();
        }
        else if (cfg.ANNO_MODE == cfg.MASS_VARY_PREDICTIVE){
            ChangeMass();
        }
    }
    
    void SetCamera() {
        CameraRandomizer cr = _Randomizers["CameraRandomizer"] as CameraRandomizer;
        CableBody[] gos = GameObject.FindObjectsOfType<CableBody>();
        List<GameObject> All = new ();
        foreach(CableBody go in gos){
            All.Add(go.gameObject);
        }
        All.AddRange(Ropes);
        cr.EncapsulateObjects(
            All,
            _maincamera.GetComponent<Camera>(),
            padding:cfg.field_paddings
        );
    }
   
    protected override void OnIterationEnd(){
        base.OnIterationEnd();
    }

    protected override void OnValidFactualDone(){
        FindMovingAgentCandidates();
        this_move = temp_static.Count;
        this_mass_change = Cubes.Count;
        _cf_iter_total = cfg.debug_cf? 0: this_move + this_mass_change;
    }

    protected override void OnCounterFactualDone(){
        cfg.ANNO_MODE = cfg.RANDOM_MASS_MOTION;
    }

    protected override void OnCounterFactualIterStart(){
        if (_this_cf_iter == this_move){
            cfg.ANNO_MODE = cfg.MASS_VARY_PREDICTIVE;
        }
        else if(_this_cf_iter == 0){
            cfg.ANNO_MODE = cfg.MOVE_STH;
        }
    }

    protected Cable.Link FixOrientation(bool enable, Cable.Link lk, int depth, int position, int idx, List<Dictionary<string, float>> ps, float uncorrect_rate = cfg.uncorrect_rate) {
        // Fix the orientation of the cable
        if (enable){
            lk.orientation = (depth % 2 == 0? 1 : -1) * (position % 2 == 0 ? 1 : -1) == -1;
            if ((idx == ps.Count - 2 || idx == 1) && position % 2 == 1) { 
                // if the last or first point is lower, reverse the orientation
                int idx0;
                if (idx == ps.Count - 2) idx0 = ps.Count - 1; else idx0 = 0;
                if (ps[idx0]["type"] == cfg.END_DYNAMIC_PULY || ps[idx0]["type"] == cfg.END_DYNAMIC_LOAD)
                    lk.orientation = !lk.orientation;
            }
        }
        if (rand_01.Sample() < uncorrect_rate) lk.orientation = !lk.orientation;
        return lk;
    }
    protected Dictionary<string, List<List<List<float>>>> _StateSaveCache = new ();
    //obsoleted function
    protected void AnswerTension() {
        Tension = new ();
        TensionAvg = new ();
        // Get the tension of the cables
        for (int j = 0; j < Ropes.Count; j++) {
            int num = 0; float sum = 0;

            Dictionary<(string, string), float> Tension_ = new ();
            GameObject rope = Ropes[j];
            Cable rp = rope.GetComponent(typeof(Cable)) as Cable;
            IList<CableJoint> joints = rp.Joints;
            for (int i = 0; i < joints.Count; i++){
                float force = joints[i].ImpulseMagnitude / Time.fixedDeltaTime;
                if (!Tension_.ContainsKey((joints[i].body1.gameObject.name, joints[i].body2.gameObject.name)))
                    Tension_.Add((joints[i].body1.gameObject.name, joints[i].body2.gameObject.name), force);
                num += 1;
                sum += force;
                Debug.Log(joints[i].body1.gameObject.name + " - " + joints[i].body2.gameObject.name + "Tension: " + force + " N, Mass:" + force/-9.81f +" Kg");
            }
            Tension.Add(rope.name, Tension_);
            TensionAvg.Add(rope.name, sum / num);
        }
    }

    protected void AnswerMotion(float motion_threshold=-1, int start_frame=-1) {
        // Answer the motion of the pulley
        MotionAnswers = new ();
        int refered_start_frame = start_frame > 0 ? start_frame : answer_refered_start_frame;
        float this_motion_threshold = motion_threshold > 0 ? motion_threshold : cfg.motion_threshold;
        foreach ((string name, GameObject obj) in _RigidbodySaveManage) {
            if (name.Contains("Rope") || name.Contains("Shaft")) continue; // should not be rope
            Debug.Log(name);
            var temp_4D = _StateSaveCache[name];
            int num = temp_4D.Count;
            float start_y = temp_4D[refered_start_frame][0][1];
            bool res = false; 
            float max = -1f;
            for (int i = refered_start_frame + 1; i < num; i++) {
                float y = temp_4D[i][0][1];
                if (Math.Abs(y - start_y) > max) {
                    max = Math.Abs(y - start_y);
                }
                if (Math.Abs(y - start_y) > this_motion_threshold) {
                    MotionAnswers.Add(name, y- start_y > 0 ? 1 : -1);
                    res = true;
                    break;
                }
            }
            Debug.Log(name + " max y motion = " + max.ToString());
            if (!res) {
                MotionAnswers.Add(name, 0);
            }
        }
    }
   
    protected void AnswerRotation(float rotation_threshold_degree=-1, int start_frame=-1) {
        // Answer the rotation of the pulley
        // SHOULD CONSIDER MIRROR EFFECT !!!
        RotationAnswers = new ();
        float thres = rotation_threshold_degree > 0 ? rotation_threshold_degree : cfg.rotation_threshold_degree;
        int refer_start_frame = start_frame > 0 ? start_frame : answer_refered_start_frame;
        foreach ((string name, GameObject obj) in _RigidbodySaveManage) {
            if (name.Contains("Pulley") && !name.Contains("Cube") && !name.Contains("Sphere") && !name.Contains("Shaft") && !name.Contains("connection") && !name.Contains("Rope")){
                var temp_4D = _StateSaveCache[name];
                int num = temp_4D.Count;
                float start_degree = temp_4D[refer_start_frame][1][2];
                bool res = false;
                for (int i = refer_start_frame + 1; i < num; i++) {
                    float degree = temp_4D[i][1][2];
                    if (degree - start_degree > 180) {
                        degree -= 360;
                    }
                    else if (degree - start_degree < -180) {
                        degree += 360;
                    }
                    if (Math.Abs(degree - start_degree) > thres) {
                        RotationAnswers.Add(name, degree - start_degree > 0 ? 1 : -1);
                        res = true;
                        // Check Mirror
                        if (mirrored){
                            RotationAnswers[name] *= -1;
                            Debug.Log("RotationAnswers[name] *= -1;");
                        };
                        break;
                    }
                }
                if (!res) {
                    RotationAnswers.Add(name, 0);
                }
            }
        }
    }
    
    protected void FindMovingAgentCandidates() {
        var all = GameObject.FindObjectsOfType<CableBody>();
        temp_static = new ();
        foreach(CableBody x in all) {
            GameObject go = x.gameObject;
            string name = go.name;
            if (name.Contains("Static") || name.Contains("Fixed")) {
                // check it should be in 1-dynamic group
                if ((!cfg.version2_only_1dyn_group_static_be_moved) || (cfg.version2_only_1dyn_group_static_be_moved && CheckStaticAgentIn1DynGroup(name)))
                    temp_static.Add(name);
            }
        }
    }

    protected bool CheckStaticAgentIn1DynGroup(string name){
        bool res = true;
        for(int i=0 ; i < AnswerRelations.Count ; i++){
            if (AnswerRelations[i].Contains(name)){
                if (!GroupIdfor1DoF.Contains(i)){
                    res = false;
                    break;
                }
            }
        }
        return res;
    }
    
    protected bool CheckMovingAgentStatic(string name) {
        int ii = -1; string keyy = "";
        for (int i=0; i<AnswerRelationsInRope.Count; i++) {
            Dictionary<string, List<string>> relation = AnswerRelationsInRope[i];
            foreach((string key, List<string> value) in relation) {
                if (value.Contains(name)) {
                    ii = i; keyy = key;
                    break;
                }
            }
            if (ii != -1) {
                break;
            }
        }
        for (int i=0; i<AnswerRelationsInRope[ii][keyy].Count; i++) {
            string name0 = AnswerRelationsInRope[ii][keyy][i];
            if ((!(name0.Contains("Static") || name0.Contains("Fixed"))) && !name0.Contains("Rope")) {
                return false;
            }
        }
        return true;
    }
    
    GameObject agentToMove;
    protected void ChooseMovingAgent() {
        QuestionMovingAgent = new();
        string name = temp_static[_this_cf_iter];
        QuestionMovingAgent.Add("name", name);
        string dir;
        // fixed point move the same dir, while static pulley move the opposite dir
        if (name.Contains("Fixed")) {
            dir = (!object_up_down[name])?"down":"up";
        }
        else {
            dir = object_up_down[name]?"down":"up";
        }
        QuestionMovingAgent.Add("direction", dir);
        QuestionMovingAgent.Add("onAllStaticRope", CheckMovingAgentStatic(temp_static[_this_cf_iter]).ToString());
        questionMovingAgentPosition = _StateSaveCache[QuestionMovingAgent["name"]][cfg.move_sth_flash_start_frame - 3][0];
        agentToMove = GameObject.Find(QuestionMovingAgent["name"]);
    }

    // public bool ismoving;
    protected void MovingAgent() {
        if (_this_frame == cfg.move_sth_flash_start_frame){
            ChooseMovingAgent();
            return;
        }
        if (_this_frame > cfg.move_sth_flash_start_frame){
            GameObject go = agentToMove;
            // object flashing effect
            int direction0 = QuestionMovingAgent["direction"] == "up" ? 1 : -1;
            if (_this_frame == cfg.move_sth_start_time){
            }
            if (_this_frame >= cfg.move_sth_start_time){
                // moving agent per update frame from the start frame on
                UnityEngine.Vector3 pos = go.transform.position;
                float scale = go.name.Contains("Fixed") ? 2 : 1;
                if (Math.Abs(pos.y - questionMovingAgentPosition[1]) > scale * cfg.moving_max_distance) {
                    return;
                }
                go.transform.position = new UnityEngine.Vector3(pos.x, pos.y + cfg.moving_speed * Time.deltaTime * direction0, pos.z);
            }
        }
    }
    
    // for mode 3
    protected void ChangeMass(){
        if (_this_frame == cfg.mass_change_start_time){
            if (Cubes.Count > 0){
                (GameObject go, int color) = Cubes[_this_cf_iter - this_move];
                float mass0 = go.GetComponent<Rigidbody>().mass;
                float scale = go.GetComponent<MassRandomizerTag>().mass_multifier;
                float mass = mass0 + scale * cfg.change_mass_categories_range[(int)(rand_01.Sample() * cfg.change_mass_categories_range.Count)];  
                go.GetComponent<Rigidbody>().mass = mass;
                QuestionMassChange_ColorMassBA = (go.name, mass0, mass);
            }
        }
    }

    // for factual stability check
    protected void CheckStability(){
        var all = GameObject.FindObjectsOfType<CableBody>();
        foreach(CableBody name in all){
            Debug.Log("Stability Check, " + name.GetComponent<Rigidbody>().velocity.magnitude);
            if (_StateSaveCache.ContainsKey(name.name)) {
                var now = _StateSaveCache[name.name][_StateSaveCache[name.name].Count-1][0];
                var pre = _StateSaveCache[name.name][0][0];
                var dist = Math.Sqrt(Math.Pow(now[0]-pre[0], 2) + Math.Pow(now[1]-pre[1], 2));
                if ((name.name.Contains("Dynamic") && dist > cfg.stability_motion_threshold) || name.GetComponent<Rigidbody>().velocity.magnitude > cfg.stability_velocity_threshold){
                    Debug.Log("Stability Check Failed");
                    if (!cfg.version1_vid_allow_all) StopThisIterImmediatelyAndInvalidIt(false);
                    return;
                }
            }
        }
    }

    //for mode 1
    protected void SetMass(){
        if (_this_frame == cfg.random_mass_motion_start_time){
            if (_this_iter_ft)
                CheckStability();
            AnswerTension();
            var rdmzr = _Randomizers["MassRandomizer"] as MassRandomizer;
            rdmzr.mass_.SetOptions(cfg.load_mass_categories_range);
            rdmzr.Sample_();
            // set down mirrored information at the factual iter period
            mirrored = (_Randomizers["CameraRandomizer"] as CameraRandomizer).mirrored;
            if (!(_Randomizers["CameraRandomizer"] as CameraRandomizer).mirror){
                mirrored = false;
            }
        }
    }

    public void StopThisIterImmediatelyAndInvalidIt(bool validity_=true, int delay=-1){
        // cubes script uses this func
        if (!done){
            if (!validity_) validity = false;
            _scenario.framesPerIteration = _this_frame + (delay>0?Math.Max(delay, 2):cfg.stopReleaseFrameN);
            done = true;
        }
        // no worry, next iter frames number will refresh
    }

}


[Serializable]
[AddComponentMenu("Rope Recorder")]
public class RopeRecorder : RandomizerTag {
    int _this_frame;
    Cable fo;

    // particles
    public List<Vector3> particleVec = new();
    public List<Vector3> entangleParticleVec = new();
    public List<List<List<float>>> perFrameParticleVec = new();
    static public bool onCapture = false; 
    public List<GameObject> tempCache = new();
    List<float> sampleAxis = new();
    private void Start() {
        _this_frame = 0;
        fo = gameObject.GetComponent<Cable>();
    }

    private void Update() {
        _this_frame ++;
        if (_this_frame == cfg.init_saving_rope_repeat_element_time)
            SetParticleAnnotation();
    }
    
    public List<List<float>> GetTrackedParticles() {
        if (onCapture){
            updateParticles();
            List<List<float>> All = new();
            All.AddRange(ParticleVecToList(particleVec));
            return All;
        }
        else {
            return new List<List<float>>();
        }
    }

    public void updateParticles(){
        var segs = fo.sampledCable.Segments[0];
        // get the segments
        if (segs.Count < 2 || sampleAxis.Count == 0) {particleVec = new(){new Vector3(0,0,0)}; return;}
        List<(Vector3, Vector3, float)> segmentPart = new(){(segs[0], segs[1], (segs[0] - segs[1]).magnitude)};
        List<float> LenList = new(){0}, PercentList = new();
        float tempLen = (segs[0] - segs[1]).magnitude;
        for (int i = 2; i < segs.Count; i++){
            var seg = segs[i];
            var seg_pred = segs[i-1];
            float len = (seg_pred - seg).magnitude;
            LenList.Add(tempLen);
            segmentPart.Add((seg_pred, segs[i], len));
            tempLen += len;
        }
        // percentlist build up
        for (int i = 0; i < LenList.Count; i++){
            PercentList.Add(LenList[i] / tempLen);
        }
        // compute the particle positions Vector3
        particleVec = new();
        int this_seg = 0;
        for (int i=0; i<sampleAxis.Count; i++){
            var pos_percent = sampleAxis[i];
            while(this_seg < PercentList.Count - 1 && PercentList[this_seg + 1] < pos_percent){
                this_seg ++;
            }
            float len = segmentPart[this_seg].Item3;
            var start = segmentPart[this_seg].Item1;
            var end = segmentPart[this_seg].Item2;
            Vector3 newPos = ((pos_percent - PercentList[this_seg]) / len * tempLen) * (end - start) + start;
            particleVec.Add(newPos);
        }

        // add the end point
        particleVec.Add(segmentPart[segmentPart.Count - 1].Item2);
    }

    public void SetParticleAnnotation(){
        Debug.Log("SetParticleAnnotation " + fo.sampledCable.Segments.Count);
        var segs = fo.sampledCable.Segments[0];
        // get the segments
        if (segs.Count < 2) {sampleAxis = new(); return;}
        float total_len = (segs[0] - segs[1]).magnitude;
        for (int i = 2; i < segs.Count; i++){
            var seg = segs[i];
            var seg_pred = segs[i-1];
            float len = (seg_pred - seg).magnitude;
            total_len += len;
        }
        // from 0 to total_len, sample per interval
        float interval = Math.Min(cfg.sampling_dist, total_len);
        int num = (int)(total_len / interval) + 1;
        sampleAxis = new();
        for (int i=0; i < num; i++){
            sampleAxis.Add(i * interval / total_len);
        }
        updateParticles();
    }

    public List<List<float>> ParticleVecToList(List<Vector3> particleVec){
        // convert the particleVec to List<List<float>>
        List<List<float>> res = new();
        for (int i = 0; i < particleVec.Count; i++){
            var pos = particleVec[i];
            res.Add(new(){pos.x, pos.y, pos.z});
        }
        return res;
    }
}
