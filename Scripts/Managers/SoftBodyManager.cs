// is added to perception camera version 3.1 dirived from baseManager
// TIPS: 
// 1. cache the objects whenever possible, try not to use GO.Find every frame
// 2. finely arrange the color, name, and transforms, parents, components of the object
// 3. add cfg file to finely arrange your config parameters.
// 4. try to divide randomization factors into different randomizers, and arrange them in this file, not all piled-up in only one manager file.
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
using SamplingFunctions;
using SoftBodyConfig;
using Unity.Profiling;
using System.Xml;
using PlacementConfig;
using Unity.Collections;
using GeometryIn2D;
using JetBrains.Annotations;
using UnityEditor.Rendering;
using System.Threading;
using UnityEngine.Perception.GroundTruth.LabelManagement;
using UnityEngine.Perception.GroundTruth.Labelers;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using UnityEngine.AI;
using Unity.Mathematics;
using UnityEditor.PackageManager.UI;
using System.Data;
using UnityEngine.Assertions;
using UnityEngine.U2D;
using System.Security.Cryptography;
using Unity.VisualScripting.FullSerializer;
using Mono.Cecil;
using System.Xml.Serialization;
using UnityEngine.Rendering.HighDefinition;
using PRUtils;
using UnityEditor.Experimental.GraphView;
using System.Security.Cryptography.X509Certificates;
using Habrador_Computational_Geometry;

public class SoftBodyTag : RandomizerTag {
    public int frame;
    public int resultPit;  // 1: left, 2: right, 3: to predict left 4: to predict right 5: predict no pit go through
    public SoftBodyManager _manager;
    private void Start() {
        frame = -1;
        resultPit = 5;
    }
    private void Update() {
        if (transform.position.y < -0.5f && frame == -1){
            frame = _manager._this_frame;
            if (transform.position.x < 0) { // left
                resultPit = 1;
            }
            else {
                resultPit = 2;
            }
            if (cfg.ANNO_MODE == cfg.FACTUAL && frame > cfg.stopCaptureTime) {
                if (transform.position.x < 0) { // left
                    resultPit = 3;
                }
                else { // right
                    resultPit = 4;
                }
            }
        }
    }
}

[Serializable]
[AddRandomizerMenu("Soft Body Manager")]
public class SoftBodyManager : BaseManager
{
    public SoftBodySampler _sampler;
    private GameObject obisolver;
    PerceptionCamera pc_an;
    List<(int, int)> this_cf_remove_wall_specified_ball_list;
    List<ObiSoftbody> AllBalls, PredBalls;
    bool done = false;

    // (this func will be called by the Main Scenario)
    // You should at least set sampling and labeling scheme
    public override void BuildScheme()
    {
        // voxel output settings
        voxel_absolute_size = cfg.voxel_absolute_size;
        realtime_voxelize  = true;

        // video saving logic settings here
        start_image_id = 2;
        end_image_id = (int)(cfg.captureFrames / (1 + cfg.framesBetweenCaptures));

        cFMode = true;
        cfg.ANNO_MODE = cfg.FACTUAL;
        _total_valid_iter = cfg.ValidIterNum;
        _framerate = cfg.frameRate;
        UnityEngine.Random.InitState(cfg.seed);

        // init others
        string idconfig_path = "PerceptionConfigs/SoftBodyIdLabelConfig";
        string ssconfig_path = "PerceptionConfigs/SoftBodySemanticSegmentationLabelConfig";
        if (Is_Both_Label_Configs_Exist_and_Not_Empty(idconfig_path, ssconfig_path))
        {
            // Init Neccessary Caches
            _scenario = GameObject.Find("Simulation").GetComponent(typeof(UnityEngine.Perception.Randomization.Scenarios.MainDataGeneration)) as UnityEngine.Perception.Randomization.Scenarios.MainDataGeneration;
            _maincamera = GameObject.Find("Main Camera");

            _sampler = new SoftBodySampler(cfg.seed);
            
            _scenario.framesPerIteration = cfg.framesPerIteration;
            _scenario.constants.randomSeed = (uint)cfg.seed;
            _scenario.constants.iterationCount = cfg.numIterations;
            _scenario.constants.startIteration = cfg.startIteration;
            // Init Randomizer
            SoftBodyManager r1 = this;
            CameraRandomizer r4 = new CameraRandomizer();
            LightRandomizer r5 = new LightRandomizer();
            r5.cam_angle_x  = new() { value = new UniformSampler(cfg.light_x_min_deg, cfg.light_x_max_deg) };
            r5.cam_angle_y  = new() { value = new UniformSampler(cfg.light_y_min_deg, cfg.light_y_max_deg) };
            // Change the Sample Parameters
            r4.lookat = cfg.lookat;
            r4.cam_angle = new() { value = new UniformSampler(cfg.cam_angle[0], cfg.cam_angle[1]) };
            r4.cam_angle2 = new() { value = new NormalSampler(cfg.cam_angle2[0], cfg.cam_angle2[1], 0, cfg.cam_angle2[1]) };
            r4.cam_radius = new() { value = new NormalSampler(cfg.cam_radius[0], cfg.cam_radius[1], cfg.cam_radius[2], cfg.cam_radius[3]) };
            r4.cam_fov  = new() { value = new UniformSampler(cfg.cam_fov[0], cfg.cam_fov[1]) };
            // Add Randomizer
            AddRandomizerAtLast("MainRandomizer", r1);
            AddRandomizerAtLast("LightRandomizer", r5);
            AddRandomizerAtLast("CameraRandomizer", r4);

            // Add Tag to Camera (others added when iter start)
            AddRandTagToObject(_maincamera, typeof(CameraRandomizerTag));
            
            pc_an = _maincamera.GetComponent<PerceptionCamera>();
            // set simulation settings 
            pc_an.simulationDeltaTime = cfg.simDeltaTime;
            pc_an.framesBetweenCaptures = cfg.framesBetweenCaptures;
            if (!cfg.annotation_Visualization) pc_an.showVisualizations = false;
            pc_an.captureRgbImages = cfg.output_RGB;
            if (cfg.output_AnnoImage){
                AddPcptCamWithLabeler(typeof(BoundingBox2DLabeler), idconfig_path);
                AddPcptCamWithLabeler(typeof(InstanceSegmentationLabeler), idconfig_path);
            }
        }
    }

    // You Should use AddObject/AddObjectFromPrefab/AddRandTagToObject/AddStateSavingObject funcs
    protected override void BuildIterScene(){
        if (_this_iter_ft){
            // resample the scene layouts
            _sampler.ResampleScene();
            // build scene
            SetObjects();
        }
        else{
            if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL){ // firster
                SetObjects(_this_cf_iter);
            }
        }
    }

    protected override List<List<float>> GetSoftbodyState(GameObject go){
        var temp = base.GetSoftbodyState(go);
        var softbody = go.GetComponent<ObiSoftbody>();
        if (softbody != null){
            // this input is fluid: output position and rotation
            var solver = obisolver.GetComponent<ObiSolver>();
            int num = softbody.solverIndices.Length;
            for (int i = 0; i<num; i++){ // local == absolute here
                int id = softbody.solverIndices[i];
                var pos = solver.positions[id];
                var pos_ls = Vec2List(pos);
                temp.Add(pos_ls);
            }
        }
        return temp;
    }

    protected override (List<List<float>>, List<int>) GetSoftbodyGeometry(GameObject go){
        (List<List<float>>, List<int>) temp = new();
        var softbody = go.GetComponent<ObiSoftbody>();
        if (softbody != null){
            SkinnedMeshRenderer skin = go.GetComponent<SkinnedMeshRenderer>();
			if (skin != null && skin.sharedMesh != null)
            {
				Mesh baked = new Mesh();
				skin.BakeMesh(baked);
				temp = Mesh2StandardFunc(baked, go.transform);
			}
        }
        return temp;
    }

    protected override void OnAwake() {
        base.OnAwake();
    }

    protected override void OnScenarioStart() {
        manualDeleteImages = true;
        base.OnScenarioStart();
        var light = AddObjectFromPrefab(cfg.light_prefab_path, cfg.light_prefab_path, new Vector3(0, 0, 0), Quaternion.identity);
        _ObjManager.Remove(cfg.light_prefab_path);
        (_Randomizers["LightRandomizer"] as LightRandomizer).light = light;
        var bg2 = AddObjectFromPrefab(cfg.bg2_prefab_path, cfg.bg2_name, new Vector3(-0.17f,3.78f,-1f), Quaternion.identity);
        bg2.transform.localScale = new Vector3(30f, 10f, 1f);
        _ObjManager.Remove(cfg.bg2_name);
        var bg = AddObjectFromPrefab(cfg.bg_prefab_path, cfg.bg_name, new Vector3(-0.17f,5f,1f), Quaternion.identity);
        bg.transform.localScale = new Vector3(30f, 12f, 1f);
        _ObjManager.Remove(cfg.bg_name);
    }

    protected override void OnIterationStart()
    {
        base.OnIterationStart();
        if (cfg.ANNO_MODE == cfg.FACTUAL){
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL){
        }
        _scenario.framesPerIteration = cfg.framesPerIteration;
        _pre_name = cfg.pre_name;
        done = false;
        // Build Scene
        BuildIterScene();
    }

    bool CheckAllSettled(){
        PredBalls = new ();
        foreach (var x in AllBalls){
            if (x.transform.position.y < -1f){ // dropped into the pit
                Debug.Log("Ball " + x.ToString() + " dropped.");
            }
            else {
                PredBalls.Add(x);
            }
        }
        return PredBalls.Count == 0;
    }

    public void StopThisIterImmediatelyAndInvalidIt(bool validity_=true){
        // cubes script uses this func
        if (!done){
            if (!validity_) validity = false;
            _scenario.framesPerIteration = _this_frame + 5;
            // no worry, next iter frames number will refresh
            done = true;
        }
    }

    protected override void OnUpdate() {
        base.OnUpdate();
        Debug.Log("Frame: " + _this_frame);
        if (cfg.ANNO_MODE == cfg.FACTUAL){ // factual mode
            if (_this_frame == 2) {
                setCamera();
            }
            if (_this_frame == cfg.stopCaptureTime) {
                _maincamera.SetActive(false); // stop capturing
            }
        }
        // left assets
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL) { // counterfactual mode
        }
        if (_this_frame > cfg.stopCaptureTime) {
            if (CheckAllSettled()) {
                StopThisIterImmediatelyAndInvalidIt(true);
            }
        }
    }

    protected override void OnIterationEnd(){
        Debug.Log("Iteration End.");
        base.OnIterationEnd();
    }

    protected Dictionary<string, object> GetAllObjectTransforms(){
        var temp = new Dictionary<string, object>();
        var all_collider = GameObject.FindObjectsOfType<ObiCollider>();
        foreach (var co in all_collider){
            string name = co.name;
            var pos = co.transform.position;
            var rot = co.transform.rotation.eulerAngles;
            var scale = co.transform.lossyScale;
            temp.Add(name, new Dictionary<string, List<float>>{
                {"position", Vec2List(pos)},
                {"rotation", Vec2List(rot)},
                {"scale", Vec2List(scale)}
            });
        }
        return temp;
    }

    public Dictionary<string, object> Summary() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        // pit drop conditions
        foreach (var x in AllBalls){
            var com = x.GetComponent<SoftBodyTag>();
            temp.Add(x.name, new Dictionary<string, object>(){{"pitResult",cfg.result_translate[com.resultPit]},{"pitFrame", (int)(com.frame/2)}});
        }
        return temp;
    }

    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        if (!validity) {  return temp; }
        // mode
        temp.Add("mode", "");
        temp.Add("trackingData", Summary());
        if (cfg.ANNO_MODE == cfg.FACTUAL){
            temp["mode"] = "FACTUAL_PREDICTIVE";
            // properties
            Dictionary<string, object> metaSamplingData = new();
            metaSamplingData.AddRange(_sampler.GetAll());
            temp.Add("metaSamplingData", metaSamplingData);
            temp.Add("allObjectTransforms", GetAllObjectTransforms());
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL){
            temp["mode"] = "COUNTERFACTUAL_";
            temp.Add("removedWall", _sampler.floatingWalls[this_cf_remove_wall_specified_ball_list[_this_cf_iter].Item1].name);
            temp.Add("ball", _sampler.balls[this_cf_remove_wall_specified_ball_list[_this_cf_iter].Item2].name);
        }
        return temp;
    }

    protected override void OnCounterFactualDone(){
        cfg.ANNO_MODE = cfg.FACTUAL;
    }
    
    protected override void OnValidFactualDone(){
        this_cf_remove_wall_specified_ball_list = new ();
        for(int i=0; i<_sampler.floatingWalls.Count; i++){
            for (int j=0; j<_sampler.balls.Count; j++){
                this_cf_remove_wall_specified_ball_list.Add((i, j));
            }
        }
        _cf_iter_total = this_cf_remove_wall_specified_ball_list.Count;
    }
    
    protected override void OnCounterFactualIterStart(){
        if (_this_cf_iter == 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL;
        }
    }

    protected void setCamera(){
        CameraRandomizer cr = _Randomizers["CameraRandomizer"] as CameraRandomizer;

        cr.lookat = cfg.lookat;
        cr.SampleCamera();
    }

    protected void SetObject_P3DMColor(GameObject go, int color){
        Material mat = go.GetComponent<MeshRenderer>().material;
        Color c = SoftColor.color_rgb_dict[color];
        mat.color = c;
        mat.SetColor("EmissiveColor", c);
    }
    
    protected GameObject AddStick(WallSampling.Stick stick){
        GameObject stick_go = AddObjectFromPrefab(cfg.stick_prefab_path, stick.name, stick.Position(), Quaternion.Euler(stick.Rotation()));
        stick_go.transform.localScale = stick.Scale();
        AddStateWriter(stick_go.name, stick_go, is_static:true);
        SetObject_P3DMColor(stick_go, stick.color);
        return stick_go;
    }

    protected void SetObjects(int toRemove=-1){
        // Build Ramp
        var right_ramp = _sampler.ramps[0];
        var left_ramp = _sampler.ramps[1];
        GameObject left_ramp_go = AddStick(left_ramp);
        GameObject right_ramp_go = AddStick(right_ramp);

        // Build floor pieces
        var floors = _sampler.floorPieces;
        int floor_num = floors.Count;
        for (int i = 0; i < floor_num; i++){
            var floor = floors[i];
            GameObject floor_go = AddStick(floor);
        }
        
        // build floating walls
        var floatingWalls = _sampler.floatingWalls;
        int floatingwall_num = floatingWalls.Count;
        for (int i = 0; i < floatingwall_num; i++){
            var wall = floatingWalls[i];
            if( toRemove >= 0 && i == this_cf_remove_wall_specified_ball_list[toRemove].Item1) continue;
            GameObject wall_go = AddStick(wall);
        }
        
        // build balls
        obisolver = AddObjectFromPrefab(cfg.solver_prefab_path, "Obi Solver", new Vector3(0, 0, 0), Quaternion.identity);
        var balls = _sampler.balls;
        int ball_num = balls.Count;
        
        AllBalls = new ();
        for (int i = 0; i < ball_num; i++){
            var ball = balls[i];
            if( toRemove < 0 || (toRemove >= 0 && i == this_cf_remove_wall_specified_ball_list[toRemove].Item2) ) {
                GameObject ball_go = AddObjectFromPrefab(cfg.elasticityType2prefabPath[ball.elasticityType], ball.name, ball.release_pos, Quaternion.identity);
                ball_go.transform.localScale = cfg.ballScale;
                AddStateWriter(ball_go.name, ball_go, is_softbody:true);
                Material mat = ball_go.GetComponent<SkinnedMeshRenderer>().material;
                Color c = SoftColor.color_rgb_dict[ball.color];
                mat.SetColor("_BASE_COLOR", c);
                ball_go.transform.parent = obisolver.transform;
                ball_go.AddComponent<SoftBodyTag>()._manager = this;
                AllBalls.Add(ball_go.GetComponent<ObiSoftbody>());
            }
        }
    }
}
