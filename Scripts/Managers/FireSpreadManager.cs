// is added to perception camera version 3.1 dirived from baseManager
// ffmpeg -f image2 -i "step%d.camera.png" output.mp4
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
using UnityEngine.Rendering;
using FireSpreadConfig;
using PlacementConfig;
using UnityEngine.VFX;
using UnityEngine.AI;
using Unity.Mathematics;
using UnityEditor.PackageManager.UI;
using UnityEngine.Perception.Randomization.Utilities;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.Rendering.HighDefinition;
using Unity.Collections;
using Ignis;
using UnityEngine.Perception.GroundTruth;
using UnityEditor.Rendering.BuiltIn.ShaderGraph;
using Unity.VisualScripting.ReorderableList.Element_Adder_Menu;
using UnityEngine.Assertions;
using System.Globalization;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using JetBrains.Annotations;
using System.Collections.ObjectModel;
using PRUtils;

[RequireComponent(typeof(Camera))]
public class FireSpreadRandomizerTag : RandomizerTag {

}

[Serializable]
[AddComponentMenu("Fire Recorder")]
public class FireRecorder : RandomizerTag, IInteractWithFire {
    int _this_frame;
    public int ignitionFrame;
    public int burnoutFrame; 
    int state; // 0 not ignited, 1 ignited, 2 burnout
    FlammableObject fo;
    public bool flammability;
    public List<string> touchers;
    public Dictionary<string, Vector3> touchPoints;
    
    public string firer;
    
    public bool isStatic = false;
    public int accum = 0;
    Rigidbody rb;

    // particles
    public List<List<float>> particleVec = new();
    public List<List<List<float>>> perFrameParticleVec = new();
    public Vector3 origin = new(0,0,0);
    static public bool onCapture = false; 

    private void Start() {
        _this_frame = 0;
        ignitionFrame = -1;
        burnoutFrame = -1;
        state = 0;
        accum = 0;
        isStatic = false;
        touchers = new ();
        touchPoints = new ();
        fo = gameObject.GetComponent<FlammableObject>();
        rb = gameObject.GetComponent<Rigidbody>();
        firer = "";
    }

    private void Update() {
        _this_frame ++;
        CheckIgnitionFrame();
        CheckBurnoutFrame();
        SetParticleAnnotation();
        CheckVelocity();
    }

    public void SampleParticles(List<List<float>> particleVec, Vector3 point, int num, float radius){
        particleVec.Add(BaseManager.Vec2List(point));
        // sample around the point within the radius sphere
        for (int i=0; i < num; i++) {
            particleVec.Add(BaseManager.Vec2List(UnityEngine.Random.onUnitSphere * radius * UnityEngine.Random.value));
        }
    }

    public void SetParticleAnnotation(){
        if (onCapture){
            if (state == 1){
                if (origin == new Vector3(0,0,0)){
                    origin = fo.GetFireOrigin();
                }
                var endA = GetEndsInWorldSpace(gameObject)[0];
                var endB = GetEndsInWorldSpace(gameObject)[1];
                var vecA = endA - origin;
                var vecB = endB - origin;
                particleVec = new();
                if(vecA.magnitude > fo.fireSpread){
                    var newPoint1 = fo.fireSpread * vecA.normalized + origin;
                    SampleParticles(particleVec, newPoint1, cfg.max_sample_particle, cfg.obj_y_scale/2);
                }
                if(vecB.magnitude > fo.fireSpread){
                    var newPoint2 = fo.fireSpread * vecB.normalized + origin;
                    SampleParticles(particleVec, newPoint2, cfg.max_sample_particle, cfg.obj_y_scale/2);
                }
                List<List<float>> this_frame_particleVec = new();
                this_frame_particleVec.AddRange(particleVec);
                if (this_frame_particleVec.Count == 0 || this_frame_particleVec[0] == null)
                    this_frame_particleVec = new(){new()};
                perFrameParticleVec.Add(this_frame_particleVec);
            }
            else {
                perFrameParticleVec.Add(new(){new()});
            }
        }
        else {
            perFrameParticleVec.Add(new(){new()});
        }
    }

    public void CheckVelocity() {
        if (!isStatic) { // is burning
            if (rb.velocity.magnitude < cfg.static_thres && rb.angularVelocity.magnitude < cfg.static_thres) {
                accum += 1;
            }
            else {
                accum = 0;
            }
            if(accum == cfg.static_judge_frames){
                isStatic = true;
            }
        }

    }

    public void CheckIgnitionFrame() {
        if (state == 0){ // last time is not ignited
            bool state1 = fo.onFire;
            if (state1) { // start burning
                state = 1;
                ignitionFrame = _this_frame;
                burnoutFrame = -2; // is burning
                if (cfg.debug_mode)
                    Debug.Log(gameObject.name +  " Ignition Frame: " + ignitionFrame);
            }
        }
    }
    
    public void CheckBurnoutFrame() {
        if (state == 1){ // last time is ignited
            bool state1 = fo.onFire;
            if (!state1) { // stop burning
                state = 2;
                burnoutFrame = _this_frame;
                if (cfg.debug_mode)
                    Debug.Log(gameObject.name +  " Burnout Frame: " + burnoutFrame);
            }
        }
    }
    
    void OnCollisionEnter(Collision collision) {
        string name0 = collision.collider.gameObject.name;
        touchers.Add(name0);
        touchPoints.Add(name0, collision.GetContact(0).point);
    }

    void OnCollisionExit(Collision collision) {
        string name0 = collision.collider.gameObject.name;
        touchers.Remove(name0);
        touchPoints.Remove(name0);
    }

    public void OnCollisionWithFire(GameObject fireObj){
        if (firer == "") {
            firer = fireObj.name;
            if (cfg.debug_mode)
                Debug.Log(gameObject.name + " is ignited by " + fireObj.name);
        }
    }

    protected List<Vector3> GetEndsInWorldSpace(GameObject obj){
        BoxCollider boxCollider = obj.GetComponent<BoxCollider>();
        Vector3[] localEndVertices = new Vector3[2];
        Vector3 center = boxCollider.center;
        Vector3 halfSize = boxCollider.size * 0.5f;

        localEndVertices[0] = center + new Vector3(-halfSize.x, 0, 0);
        localEndVertices[1] = center + new Vector3(halfSize.x, 0, 0);

        return new List<Vector3>(){obj.transform.TransformPoint(localEndVertices[0]), obj.transform.TransformPoint(localEndVertices[1])};
    }

}




[Serializable]
[AddRandomizerMenu("Fire Spread Manager")]
public class FireSpreadManager : BaseManager
{
    // TODO Parameters and Caches: 
    GameObject m_Container;
    GameObject floor;
    GameObject trigger;
    PerceptionCamera pc_an;
    // PerceptionCamera pc_im;
    string m_Container_name = "m_container";
    int woodCountThisIter;
    public bool isignited;

    public UnityEngine.Perception.Randomization.Parameters.FloatParameter _scale_x_length = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(cfg.obj_x_scale_min, cfg.obj_x_scale_max) };// The scale of the placed objects
    public UnityEngine.Perception.Randomization.Parameters.Vector3Parameter _rotation = new(); /// The rotation of the placed objects
    private UnityEngine.Perception.Randomization.Parameters.FloatParameter rand01 = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(0.0f, 1.0f) };
    private UnityEngine.Perception.Randomization.Parameters.IntegerParameter _woodCount = new UnityEngine.Perception.Randomization.Parameters.IntegerParameter() { value = new UniformSampler(cfg.wood_count_min, cfg.wood_count_max) };
    private UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<float> _xfireCrawlSpeed = new();
    private UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<float> _xdurationTime = new();

    Dictionary<string, float> fireCrawlSpeed = new ();
    Dictionary<string, float> durationTime = new (); // burnout Start s
    Dictionary<string, int> flammability = new (); // 0 not flammable, 1 flammable (2 half flammable
    Dictionary<string, int> startIgnitionTime = new (); // -1 means not ignited. unflammable may have >0 stratIgnitionTime to verify its collision of fire
    Dictionary<string, int> endIgnitionTime = new (); // -1 means not ignited or not flammable, (if needed, -2 means ignited but not burnout until the capture ends

    Dictionary<string, List<float>> objPos = new ();
    Dictionary<string, List<float>> objRot = new ();
    Dictionary<string, List<float>> objScale = new ();
    Dictionary<string, List<string>> objRelation = new ();

    List<FireRecorder> leftFireRecorders = new List<FireRecorder>();
    
    Dictionary<string, object> metaSampleData = new();
    int woodCount; 
    Dictionary<string, Dictionary<string, float>> woodData = new ();
    Dictionary<string, Dictionary<string, float>> woodPredData = new ();
    Dictionary<string, float> triggerData = new ();
    Dictionary<string, float> bridgeData = new ();
    string bridgeName;
    string firstIgniteWoodName;
    Dictionary<string, int> touchGroups;
    Dictionary<string, int> rightTouchGroups;
    Dictionary<string, List<string>> flammableGroups;
    Dictionary<string, List<string>> rightFlammableGroups;
    List<float> ignitionTriggerXYZ;
    bool done;
    List<string> UnableWoodList;
    int IgniteTime;
    Dictionary<string, FireRecorder> AllFireRecorders;
    bool iscapturing;
    string CF_WoodUnableFlammability;
    int this_unable, this_trigger;
    public Dictionary<string, string> fireIgniteRelations;


    // Define How to Build Randomizers/Tags/PerceptionLabeler At Scenario's OnAwake() func
    // (this func will be called by the Main Scenario)
    // You should at least set sampling and labeling scheme
    public override void BuildScheme()
    {
        //video saving logic settings here
        start_image_id = 2;
        end_image_id = 1000;

        // cF Mode
        cFMode = true;
        cfg.ANNO_MODE = cfg.FACTUAL;
        _total_valid_iter = cfg.ValidIterNum;
        _framerate = cfg.frameRate;
        // init categories
        UnityEngine.Random.InitState(cfg.randomSeed);
        _rotation = new () {x = new UniformSampler(cfg.obj_rotation_x_min, cfg.obj_rotation_x_max), 
                            y = new UniformSampler(cfg.obj_rotation_y_min, cfg.obj_rotation_y_max), 
                            z = new UniformSampler(cfg.obj_rotation_z_min, cfg.obj_rotation_z_max)};
        _xfireCrawlSpeed.SetOptions(cfg.fireCrawlSpeed);
        _xdurationTime.SetOptions(cfg.fireDurationTime);
        // init others
        string idconfig_path = "PerceptionConfigs/FireSpreadIdLabelConfig";
        string ssconfig_path = "PerceptionConfigs/FireSpreadSemanticSegmentationLabelConfig";
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
            FireSpreadManager r1 = this;
            CameraRandomizer r5 = new CameraRandomizer();
            
            // Change the Sample Parameters
            r5.cam_angle = new(){ value = new UniformSampler(cfg.cam_angle_min, cfg.cam_angle_max) };
            r5.cam_angle2 = new(){ value = new UniformSampler(cfg.cam_angle2_min, cfg.cam_angle2_max) };
            r5.cam_radius = new(){ value = new UniformSampler(cfg.cam_radius_min, cfg.cam_radius_max) };
            r5.cam_fov = new(){ value = new UniformSampler(cfg.cam_fov_min, cfg.cam_fov_max) };
            r5.lookat = cfg.cam_lookat;
            
            // Add Randomizer
            AddRandomizerAtLast("MainRandomizer", r1);
            AddRandomizerAtLast("CameraRandomizer", r5);
            
            // Add Tag to Camera (others added when iter start)
            AddRandTagToObject(_maincamera, typeof(CameraRandomizerTag));

            // use two cameras to capture images and annotations respectively
            pc_an = _maincamera.GetComponent<PerceptionCamera>();

            // set camera settings
            pc_an.simulationDeltaTime = cfg.simDeltaTime;
            pc_an.framesBetweenCaptures = cfg.framesBetweenCaptures;
            if (!cfg.annotation_Visualization) pc_an.showVisualizations = false;

            // Add Labeler and Set Config File
            if (cfg.output_AnnoImage){   
                AddPcptCamWithLabeler(typeof(BoundingBox2DLabeler), idconfig_path);
                AddPcptCamWithLabeler(typeof(InstanceSegmentationLabeler), idconfig_path);
            }
        }    
    }

    protected List<Vector3> GetEndsInWorldSpace(GameObject obj){
        BoxCollider boxCollider = obj.GetComponent<BoxCollider>();
        Vector3[] localEndVertices = new Vector3[2];
        Vector3 center = boxCollider.center;
        Vector3 halfSize = boxCollider.size * 0.5f;

        localEndVertices[0] = center + new Vector3(-halfSize.x, 0, 0);
        localEndVertices[1] = center + new Vector3(halfSize.x, 0, 0);

        return new List<Vector3>(){obj.transform.TransformPoint(localEndVertices[0]), obj.transform.TransformPoint(localEndVertices[1])};
    }

    protected bool TouchGroups(){
        touchGroups = new ();
        flammableGroups = new ();
        int id = 0;
        foreach (string obj in objRelation.Keys){
            if (obj.Contains("Wood") && !obj.Contains("Right")){
                if (touchGroups.ContainsKey(obj) || flammability[obj] == 0) continue;
                touchGroups.Add(obj, id);
                AddTouchesExceptUnflammable(objRelation[obj], id);
                id += 1;
            }
        }
        foreach ((string obj, int group) in touchGroups){
            if(flammableGroups.ContainsKey(group.ToString()))
                flammableGroups[group.ToString()].Add(obj);
            else
                flammableGroups.Add(group.ToString(), new List<string>(){obj});
        }

        rightTouchGroups = new ();
        rightFlammableGroups = new ();
        int id1 = 0;
        foreach (string obj in objRelation.Keys){
            if (obj.Contains("Wood") && !obj.Contains("Left")){
                if (rightTouchGroups.ContainsKey(obj) || flammability[obj] == 0) continue;
                rightTouchGroups.Add(obj, id1);
                AddTouchesExceptUnflammableRight(objRelation[obj], id1);
                id1 += 1;
            }
        }
        foreach ((string obj, int group) in rightTouchGroups){
            if(rightFlammableGroups.ContainsKey(group.ToString()))
                rightFlammableGroups[group.ToString()].Add(obj);
            else
                rightFlammableGroups.Add(group.ToString(), new List<string>(){obj});
        }
        
        // check if unflammable can be touched with flammables
        bool isOK = true;
        foreach (string obj in objRelation.Keys){
            if (obj.Contains("Wood") && !obj.Contains("Right")){
                if (flammability[obj] == 0){
                    bool isTouch = false;
                    foreach (string neighbor in objRelation[obj]){
                        if (neighbor.Contains("Wood") && !neighbor.Contains("Right")){
                            if (flammability[neighbor] == 1){
                                isTouch = true;
                                break;
                            }
                        }
                    }
                    if (!isTouch){
                        isOK = false;
                        break;
                    }
                }
            }
        }

        // check if left and right woods doesn't contact each other
        bool isLeftRightUntouched = true;
        foreach (string obj in objRelation.Keys){
            if (obj.Contains("Wood") && !obj.Contains("Right") && !obj.Contains("Bridge")){
                foreach (string neighbor in objRelation[obj]){
                    if (neighbor.Contains("Wood") && neighbor.Contains("Right")){
                        isLeftRightUntouched = false;
                        break;
                    }
                }
                if (!isLeftRightUntouched) break;
            }
        }
        // give the judgement result
        if(flammableGroups.Count == 1 && isOK && isLeftRightUntouched) return true;
        else return false;
    }

    protected void AddTouchesExceptUnflammable(List<string> neighbors, int id){
        foreach (string neighbor in neighbors){
            if (neighbor.Contains("Wood") && !neighbor.Contains("Right")){
                if (!touchGroups.ContainsKey(neighbor) && flammability[neighbor] == 1){
                    touchGroups.Add(neighbor, id);
                    AddTouchesExceptUnflammable(objRelation[neighbor], id);
                }
            }
        }
    }

    protected void AddTouchesExceptUnflammableRight(List<string> neighbors, int id){
        foreach (string neighbor in neighbors){
            if (neighbor.Contains("Wood") && !neighbor.Contains("Left")){
                if (!rightTouchGroups.ContainsKey(neighbor) && flammability[neighbor] == 1){
                    rightTouchGroups.Add(neighbor, id);
                    AddTouchesExceptUnflammableRight(objRelation[neighbor], id);
                }
            }
        }
    }

    protected bool IsUpperSpaceVoid(string avoidName, Vector3 point, float liftY, float maxDistance, float deltaXorZ) {
        Vector3 direction = Vector3.up;
        List<int> temp = new List<int>(){-1, 0, 1};
        for(int i=0;i<3;i++){
            for (int j=0;j<3;j++){
                RaycastHit[] a = Physics.RaycastAll(new Ray(point + new Vector3(deltaXorZ * temp[i], liftY, deltaXorZ * temp[j]), direction), maxDistance, -1);
                if(!(a.Length == 0 || (a.Length == 1 && a[0].collider.gameObject.name == avoidName))){
                    return false;
                }
            }
        }
        return true;
    }

    protected void SampleScheme(){
        woodData = new();
        woodPredData = new();
        // sample the positions
        List<int> color_candidates = new List<int>();
        color_candidates.AddRange(cfg.wood_color_range);
        // shuffle the list
        for (int i = 0; i < color_candidates.Count; i++) {
            int temp = color_candidates[i];
            int randomIndex = UnityEngine.Random.Range(i, color_candidates.Count);
            color_candidates[i] = color_candidates[randomIndex];
            color_candidates[randomIndex] = temp;
        }
        var seed = SamplerState.NextRandomState();
        var placementSamples = PoissonDiskSampling.GenerateSamples(cfg.placementArea.x, cfg.placementArea.y, cfg.seperationDistance, seed);
        var offset = new Vector3(cfg.placementArea.x, 0f, cfg.placementArea.y) * -0.5f;
        float _height = cfg.first_wood_height;

        int count = 0, total = _woodCount.Sample();
        woodCount = Math.Min(placementSamples.Length, total);
        int bridgeIdx = (int)((woodCount + 1) * rand01.Sample());
        float bridgeHeight = 0f;
        if (bridgeIdx == 0) {
            bridgeHeight = _height;
            _height += cfg.other_wood_height_step;
        }
        bool allSameCrawlSpeed = true;
        float sameCrawlSpeed = -1;
        foreach (var sample in placementSamples) {
            int colorid = color_candidates[count];
            float scale_x = _scale_x_length.Sample();
            float scale_y = cfg.obj_y_scale;
            float scale_z = cfg.obj_z_scale;
            Vector3 position = new Vector3(sample.x + cfg.campFire_left_x_offset, _height, sample.y) + offset;
            _height += cfg.other_wood_height_step;
            Vector3 Rotate = new Vector3(_rotation.x.Sample(), _rotation.y.Sample(), _rotation.z.Sample());
            // fire crawl speed
            float fcs = _xfireCrawlSpeed.Sample();
            if (sameCrawlSpeed < 0) {
                sameCrawlSpeed = fcs;
            }
            else if (sameCrawlSpeed != fcs) {
                allSameCrawlSpeed = false;
            }
            // fire duration time
            float fdt = _xdurationTime.Sample();
            // fire flammability
            int flammability_0 = 1;
            if (rand01.Sample() < cfg.unflammable_rate){ // unflammable
                flammability_0 = 0;
            }
            woodData.Add("Left " + SoftColor.color_name_dict[colorid] + " Wood",
                        new Dictionary<string, float>(){
                            {"x", position.x},
                            {"y", position.y},
                            {"z", position.z},
                            {"scale_x", scale_x},
                            {"scale_y", scale_y},
                            {"scale_z", scale_z},
                            {"rotate_x", Rotate.x},
                            {"rotate_y", Rotate.y},
                            {"rotate_z", Rotate.z},
                            {"color", colorid},
                            {"position", 0},
                            {"fireCrawlSpeed", fcs},
                            {"durationTime", fdt},
                            {"flammability", flammability_0}
            });
            count++;
            if (count == bridgeIdx){
                bridgeHeight = _height;
                _height += cfg.other_wood_height_step;
            }
            if (count == total) {
                break;
            }
        }
        Assert.IsTrue(woodCount == count, "Code Wrong");
        placementSamples.Dispose();
        // avoid same crawl speed
        if (allSameCrawlSpeed) {
            float newCrawlSpeed = _xfireCrawlSpeed.Sample();
            while (newCrawlSpeed == sameCrawlSpeed) {
                newCrawlSpeed = _xfireCrawlSpeed.Sample();
            }
            string next = woodData.Keys.ToList()[(int)(rand01.Sample() * woodData.Count)];
            woodData[next]["fireCrawlSpeed"] = newCrawlSpeed;
        } 
        // first ignite wood
        int first_ignite_wood = (int)(rand01.Sample() * woodCount);
        firstIgniteWoodName = "Left " + SoftColor.color_name_dict[color_candidates[first_ignite_wood]] + " Wood";
        woodData[firstIgniteWoodName]["flammability"] = 1;

        // CF - unable wood list
        UnableWoodList = new List<string>();
        foreach (string wood in woodData.Keys){
            if (woodData[wood]["flammability"] == 1){
                UnableWoodList.Add(wood.Replace("Left ", ""));
            }
        }
        
        // CF - trigger position List
        // in fine

        //bridge
        bridgeData = new();
        bridgeData.Add("x", 0f);
        bridgeData.Add("y", bridgeHeight);
        bridgeData.Add("z", 0f);
        bridgeData.Add("scale_x", cfg.bridge_scale_x);
        bridgeData.Add("scale_y", cfg.obj_y_scale);
        bridgeData.Add("scale_z", cfg.obj_z_scale);
        bridgeData.Add("rotate_x", 0f);
        bridgeData.Add("rotate_y", 0f);
        bridgeData.Add("rotate_z", 0f);
        bridgeData.Add("color", color_candidates[woodCount]);
        bridgeData.Add("fireCrawlSpeed", cfg.bridge_fireCrawlSpeed);
        bridgeData.Add("durationTime", cfg.bridge_durationTime);
        bridgeData.Add("flammability", 1);
        bridgeName = "Bridge " + SoftColor.color_name_dict[color_candidates[woodCount]] + " Wood";
        // right campfire
        var seed1 = SamplerState.NextRandomState();
        var placementSamples1 = PoissonDiskSampling.GenerateSamples(cfg.placementAreaRiCamp.x, cfg.placementAreaRiCamp.y, cfg.seperationDistanceRiCamp, seed1);
        while (placementSamples1.Length < woodCount){
            seed1 = SamplerState.NextRandomState();
            placementSamples1 = PoissonDiskSampling.GenerateSamples(cfg.placementAreaRiCamp.x, cfg.placementAreaRiCamp.y, cfg.seperationDistanceRiCamp, seed1);
        }
        var offset1 = new Vector3(cfg.placementAreaRiCamp.x, 0f, cfg.placementAreaRiCamp.y) * -0.5f;
        float _heightFromBridge = bridgeHeight;
        int bridgeIdx1 = (int)((woodCount + 1) * rand01.Sample());
        int downNum = bridgeIdx1, upNum = woodCount - bridgeIdx1;
        List<int> IdxPool = new List<int>();
        IdxPool.AddRange(Enumerable.Range(0, woodCount));
        List<string> woodName = new List<string>();
        woodName = woodData.Keys.ToList();
        // down
        float _height_min = 10000;
        for (int i = 0; i < woodCount; i++) {
            int idx = IdxPool[(int)(IdxPool.Count * rand01.Sample())];
            IdxPool.Remove(idx);
            Dictionary<string, float> dict = woodData[woodName[idx]];
            int colorid = (int)dict["color"];
            float scale_x = _scale_x_length.Sample(); //resample
            float scale_y = cfg.obj_y_scale;
            float scale_z = cfg.obj_z_scale;
            float y = i < downNum ? _heightFromBridge - (i + 1) * cfg.other_wood_height_step 
                                    : _heightFromBridge + (i - downNum + 1) * cfg.other_wood_height_step;
            Vector3 position = new Vector3(placementSamples1[i].x + cfg.campFire_right_x_offset, y, 
                                                placementSamples1[i].y) + offset1;
            Vector3 Rotate = new Vector3(_rotation.x.Sample(), _rotation.y.Sample(), _rotation.z.Sample());
            woodPredData.Add("Right " + SoftColor.color_name_dict[colorid] + " Wood",
                            new Dictionary<string, float>(){
                                {"x", position.x},
                                {"y", position.y},
                                {"z", position.z},
                                {"scale_x", scale_x},
                                {"scale_y", scale_y},
                                {"scale_z", scale_z},
                                {"rotate_x", Rotate.x},
                                {"rotate_y", Rotate.y},
                                {"rotate_z", Rotate.z},
                                {"color", colorid},
                                {"position", 1},
                                {"fireCrawlSpeed", dict["fireCrawlSpeed"]},
                                {"durationTime", dict["durationTime"]},
                                {"flammability", dict["flammability"]}
            });
            _height_min = Mathf.Min(_height_min, y);
        }
        placementSamples1.Dispose();
        // lift up the ground
        float up_height = Math.Max(cfg.first_wood_height - _height_min, 0);
        foreach(var wood in woodPredData){
            wood.Value["y"] += up_height;
        }
        foreach(var wood in woodData){
            wood.Value["y"] += up_height;
        }
        bridgeData["y"] += up_height;

        // generate a trigger point
        triggerData = new();
    }

    protected void SetWoods(bool Replay = false, string WoodUnable = "adffdfasadsfasdfad"){
        // we need to set the camera false;
        _maincamera.SetActive(false);
        fireCrawlSpeed = new ();
        durationTime = new ();
        flammability = new ();
        startIgnitionTime = new ();
        endIgnitionTime = new ();
        m_Container = GameObject.Find(m_Container_name);
        if (m_Container == null){
            m_Container = new GameObject(m_Container_name);
        }
        AddObject(m_Container);
        m_Container.transform.parent = scenario.transform;
        Dictionary<string, Dictionary<string, float>> woodDataAll = new();
        woodDataAll.AddRange(woodData);
        woodDataAll.AddRange(woodPredData);
        woodDataAll.Add(bridgeName, bridgeData);
        if (_this_iter_ft) {if (!_RigidbodySaveManage.ContainsKey(floor.name)) AddStateWriter(floor.name, floor, is_static:true);}
        // add objects
        foreach ((string name_left, Dictionary<string, float> data) in woodDataAll) { 
            GameObject instance = AddObjectFromPrefab(cfg.wood_prefab_path, name_left);
            int colorid = (int)data["color"];
            instance.name = name_left;
            instance.GetComponent<Renderer>().material.SetColor("_BaseColor", SoftColor.color_rgb_dict[colorid]);
            if (Replay){
                var pos = objPos[name_left]; 
                var rot = objRot[name_left];
                var scale = objScale[name_left];
                instance.transform.position = new Vector3(pos[0], pos[1], pos[2]);
                instance.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
                instance.transform.rotation = Quaternion.Euler(new Vector3(rot[0], rot[1], rot[2]));
            }
            else {
                instance.transform.position = new Vector3(data["x"], data["y"], data["z"]);
                instance.transform.localScale = new Vector3(data["scale_x"], data["scale_y"], data["scale_z"]);
                instance.transform.Rotate(new Vector3(data["rotate_x"], data["rotate_y"], data["rotate_z"]));
            }
            instance.transform.parent = m_Container.transform;
            if (_this_iter_ft){
                AddStateWriter(instance.name, instance, is_static:true);
                AddStateWriter("Fire of " + instance.name, instance, is_softbody:true);
            }
            FlammableObject fo = instance.GetComponent<FlammableObject>();
            
            string name = instance.name;
            // burnout color
            fo.shaderBurntColor = instance.GetComponent<Renderer>().material.GetColor("_BaseColor");
            fo.colorIntensityMultiplier = cfg.colorIntensityMultiplier;
            fo.fireColor = cfg.fireColorNew;
            fo.fireColorBlend = cfg.fireColorBlend;
            fo.flameLength = cfg.flameLength;
            fo.flameVFXMultiplier = cfg.flameVFXMultiplier;
            fo.flameEnvironmentalSpeed = cfg.flameEnvironmentalSpeed;
            fo.flameLiveliness = cfg.flameLiveliness;
            fo.flameLivelinessSpeed = cfg.flameLivelinessSpeed;
            fo.flameParticleSize = cfg.flameParticleSize;

            // fire crawl speed
            float fcs = data["fireCrawlSpeed"];
            fo.fireCrawlSpeed = fcs;
            fireCrawlSpeed.Add(name, fcs);
            // fire duration time
            float fdt = data["durationTime"];
            fo.burnOutStart_s = fdt;
            durationTime.Add(name, fdt);
            // fire effect space bbox
            fo.flameCatchAreaAddition = cfg.fireBboxSpaceAddition;

            // fire flammability
            if (data["flammability"] == 0 || name.Contains(WoodUnable)){
                // unflammable
                fo.maxSpread = cfg.maxSpreadUnflammable;
                fo.burnOutStart_s = cfg.maxDuration;
                flammability.Add(name, 0);
                // Add FireRecorder to record the event, state and time
                instance.AddComponent<FireRecorder>().flammability = false;
            }
            else{
                flammability.Add(name, 1);
                instance.AddComponent<FireRecorder>().flammability = true;
            }
            startIgnitionTime.Add(name, -1);
            endIgnitionTime.Add(name, -1);   
        }
    }
    
    // sample point on A LINE(x-z axis) to avoid too close to other lines
    protected float SamplePointOnLine(List<Vector3> linePoints, List<List<Vector3>> otherLines, float threshold)
    {
        int loopCount = 0;
        while (loopCount++<30)
        {
            // Sample a point on line A
            float alpha = rand01.Sample();
            float[] pointOnLine = new float[] { alpha * (linePoints[1].x - linePoints[0].x) + linePoints[0].x,
                                                alpha * (linePoints[1].z - linePoints[0].z) + linePoints[0].z };

            // Check the distance between line A and all other lines
            bool tooClose = false;
            foreach (List<Vector3> otherLine in otherLines)
            {
                float distance = (float)Math.Abs((otherLine[1].x - otherLine[0].x) * (pointOnLine[1] - otherLine[0].z) -
                                           (otherLine[1].z - otherLine[0].z) * (pointOnLine[0] - otherLine[0].x)) /
                                   (float)Math.Sqrt(Math.Pow(otherLine[1].x - otherLine[0].x, 2) + Math.Pow(otherLine[1].z - otherLine[0].z, 2));
                if (distance < threshold)
                {
                    tooClose = true;
                    break;
                }
            }

            // If the point is not too close to any other line, add it to the result list and return it
            if (!tooClose)
            {
                return alpha;
            }
        }
        return -1;
    }

    protected bool fineIgnition(){
        trigger = GameObject.Find(firstIgniteWoodName);
        List<List<Vector3>> otherLines = new List<List<Vector3>>();
        List<Vector3> pointOnLine = GetEndsInWorldSpace(trigger);
        foreach (string name in woodData.Keys){
            if (name == trigger.name) continue;
            GameObject wood = GameObject.Find(name);
            List<Vector3> ends = GetEndsInWorldSpace(wood);
            otherLines.Add(ends);
        }
        otherLines.Add(GetEndsInWorldSpace(GameObject.Find(bridgeName)));

        float alpha = SamplePointOnLine(pointOnLine, otherLines, cfg.triggerSpareSpaceThreshold);

        if (alpha >= -0.5) {
            ignitionTriggerXYZ = new List<float>(){alpha * (pointOnLine[1].x - pointOnLine[0].x) + pointOnLine[0].x,
                                                    alpha * (pointOnLine[1].y - pointOnLine[0].y) + pointOnLine[0].y,
                                                    alpha * (pointOnLine[1].z - pointOnLine[0].z) + pointOnLine[0].z};
            return true;
        }
        else return false;
    }

    List<(string, float, float, float)> triggerCandidates= new ();
    (string, float, float, float) triggerCounterfact;
    protected void setTriggerPointCandidates(){
        triggerCandidates = new();
        Dictionary<string, Dictionary<string, float>> Total = new ();
        Total.AddRange(woodData);
        Total.AddRange(woodPredData);
        Total.Add(bridgeName, bridgeData);
        foreach(string name in Total.Keys){
            GameObject wood = GameObject.Find(name);
            List<Vector3> ends = GetEndsInWorldSpace(wood);
            if (Math.Abs(ends[0].x - ends[1].x) < cfg.describeXTooCloseThreshold) {
                int forwards_seq = ends[0].z < ends[1].z ? 0 : 1;
                int backwards_seq = 1-forwards_seq;
                // if upper space no other objects, add to candidates
                // Note: only one situation for gen speed delete else to make full
                if (IsUpperSpaceVoid(name, ends[forwards_seq], 0, cfg.trigger_height + 1, cfg.trigger_scale*2/3))
                    triggerCandidates.Add(("Near-to-Viewer End of the " + name, ends[forwards_seq].x, ends[forwards_seq].y + cfg.trigger_height, ends[forwards_seq].z));
                else if (IsUpperSpaceVoid(name, ends[backwards_seq], 0, cfg.trigger_height + 1, cfg.trigger_scale*2/3))
                    triggerCandidates.Add(("Far-from-Viewer End of the " + name, ends[backwards_seq].x, ends[backwards_seq].y + cfg.trigger_height, ends[backwards_seq].z));
            }
            else{
                int left_seq = ends[0].x < ends[1].x ? 0 : 1;
                int right_seq = 1-left_seq;
                // if upper space no other objects, add to candidates  
                // Note: only one situation for gen speed delete else to make full
                if (IsUpperSpaceVoid(name, ends[left_seq], 0, cfg.trigger_height + 1, cfg.trigger_scale*2/3))
                    triggerCandidates.Add(("Left End of the " + name, ends[left_seq].x, ends[left_seq].y + cfg.trigger_height, ends[left_seq].z));
                else if (IsUpperSpaceVoid(name, ends[right_seq], 0, cfg.trigger_height + 1, cfg.trigger_scale*2/3))
                    triggerCandidates.Add(("Right End of the " + name, ends[right_seq].x, ends[right_seq].y + cfg.trigger_height, ends[right_seq].z));
            }
        }
    }

    protected void SetIgnition(bool counterfactual = false){
        // generate a trigger point
        Vector3 pos;
        if(counterfactual) {
            pos = new Vector3(triggerCounterfact.Item2, triggerCounterfact.Item3, triggerCounterfact.Item4);
        }
        else {
            pos = new Vector3(ignitionTriggerXYZ[0], ignitionTriggerXYZ[1] + cfg.trigger_height, ignitionTriggerXYZ[2]);
        }

        GameObject instance1 = AddObjectFromPrefab(cfg.trigger_prefab_path, "Trigger");
        AddStateWriter(instance1.name, instance1);
        instance1.transform.position = pos;
        instance1.transform.localScale = new Vector3(1, 1, 1) * cfg.trigger_scale;
        // instance.transform.parent = m_Container.transform;
        // set name of the triger
        instance1.name = cfg.trigger_name;
        trigger = instance1;
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
    
    protected override void BuildIterScene(){
        if ( _this_iter_ft){
            SampleScheme();
            // build scene
            SetWoods();
        }
        else{
            if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_UNABLE){
                // build scene
                SetWoods(Replay:true, UnableWoodList[_this_cf_iter]);
            }
            else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_CHANGETRIGGER){
                // build scene
                SetWoods(Replay:true);
            }
        }
    }

    public void GetFireRelations(){
        // get all the firers
        fireIgniteRelations = new ();
        foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
            fireIgniteRelations.Add(tag.name, tag.firer);
        }
    }

    public void GetFireEventTimes(){
        foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
            string name = tag.gameObject.name;
            if (tag.flammability){ // if unflammable, value = -1
                startIgnitionTime[name] = tag.ignitionFrame;
                endIgnitionTime[name] = tag.burnoutFrame;
            }
        }
    }

    public override void SaveAll(string path_without_slash){
        foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
            string name = tag.gameObject.name;
            startIgnitionTime[name] = tag.ignitionFrame;
            endIgnitionTime[name] = tag.burnoutFrame;
        }
        // mode
        File.WriteAllText(path_without_slash + "/" + "output_mode.json", JsonConvert.SerializeObject(cfg.ANNO_MODE));
        // properties
        File.WriteAllText(path_without_slash + "/" + "output_fireCrawlSpeed.json", JsonConvert.SerializeObject(fireCrawlSpeed));
        File.WriteAllText(path_without_slash + "/" + "output_durationTime.json", JsonConvert.SerializeObject(durationTime));
        File.WriteAllText(path_without_slash + "/" + "output_flammability.json", JsonConvert.SerializeObject(flammability));
        File.WriteAllText(path_without_slash + "/" + "output_startIgnitionTime.json", JsonConvert.SerializeObject(startIgnitionTime));
        File.WriteAllText(path_without_slash + "/" + "output_endIgnitionTime.json", JsonConvert.SerializeObject(endIgnitionTime));
        // saving states
        File.WriteAllText(path_without_slash + "/" + "output_wood_pos.json", JsonConvert.SerializeObject(objPos));
        File.WriteAllText(path_without_slash + "/" + "output_wood_rot.json", JsonConvert.SerializeObject(objRot));
        File.WriteAllText(path_without_slash + "/" + "output_wood_scale.json", JsonConvert.SerializeObject(objScale));
        File.WriteAllText(path_without_slash + "/" + "output_wood_relations.json", JsonConvert.SerializeObject(objRelation));
    }
    
    // public void BridgeFireSpeedControl(){

    public void CheckStartIgnition(){
        if(!isignited && _this_frame >= cfg.IgniteTime && _this_frame % 10 == 0){
            if (AllStatic()){
                StateCapture();
                bool fineLayout = TouchGroups();
                bool fineIgnition = this.fineIgnition();
                if (fineLayout && fineIgnition){
                    // set candidates for trigger change in cf mode
                    setTriggerPointCandidates();
                    // set ignition
                    _maincamera.SetActive(true);
                    setCamera();
                    // SetIgnition();
                    isignited = true;
                    isEndingCapturing = false;
                    iscapturing = true;
                    FireRecorder.onCapture = true;
                    IgniteTime = _this_frame;
                    SetDropTime = IgniteTime + cfg.PrepareDropTime;
                }
                else{
                    _this_frame = 0;
                    validity = true;
                    // Destroy Objects
                    foreach ((string key, GameObject go) in _ObjManager){ GameObject.DestroyImmediate(go); }
                    time_seq = new List<float>();
                    _ObjManager = new Dictionary<string, GameObject>();
                    base.RefreshCacheEachIter();
                    // resample
                    SampleScheme();
                    // build scene
                    SetWoods();
                }
            }
            else if (_this_frame > cfg.MaxPermitRestTime){ // to avoid the cases of long-time unstable states
                StopThisIterImmediatelyAndInvalidIt(false);
            }
        }
    }
    bool setBridgeSlow;
    int FrameNumber;
    bool isEndingCapturing;
    public void CheckToStopCapture(){
        if (isignited && iscapturing){
            if (_this_frame == SetDropTime + 1){
                setBridgeSlow = false;
                leftFireRecorders = new ();
                foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
                    if (tag.gameObject.name == cfg.trigger_name) continue;
                    if (tag.name.Contains("Left")) leftFireRecorders.Add(tag);
                }
            }
            if (_this_frame > SetDropTime + 1 && !setBridgeSlow){
                bool leftAllIgnited = true;
                foreach(FireRecorder tag in leftFireRecorders){
                    if(tag.burnoutFrame == -1){
                        // not ignited
                        leftAllIgnited = false;
                        break;
                    }
                }
                if (leftAllIgnited){ 
                    // slow down the brige fire crawling speed
                    GameObject.Find(bridgeName).GetComponent<FlammableObject>().fireCrawlSpeed = cfg.bridge_fireCrawlSpeedSlow;
                    setBridgeSlow = true;
                }
            }
            if (_this_frame == SetDropTime + cfg.MaxDropTime){
                // check if all fire is ignited
                bool anyoneIgnited = false;
                foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
                    if (tag.gameObject.name == cfg.trigger_name) continue;
                    if ((!(tag.ignitionFrame == -1 && tag.burnoutFrame == -1)) && tag.flammability) {
                        anyoneIgnited = true;
                    }
                }
                if (!anyoneIgnited){
                    // nothing ignited, so stop this iter
                    StopThisIterImmediatelyAndInvalidIt(false);
                }
            }
            else if (!isEndingCapturing && _this_frame % 20 == 0 && _this_frame >= SetDropTime + cfg.MinBurningTime) {
                if (_this_frame >= SetDropTime + cfg.MaxPermitBurningTime) {
                    // cannot capturing the left camp firing too long time, maybe something wrong
                    StopThisIterImmediatelyAndInvalidIt(false);
                    Debug.Log("cannot capturing the left camp firing too long time, maybe something wrong");
                }
                bool unFinish = false; // isBurning = false;
                List<GameObject> toBurn = new List<GameObject>();
                foreach (FireRecorder tag in leftFireRecorders){
                    if (tag.ignitionFrame < 0){
                        unFinish = true;
                    }
                }
                if (AllFireRecorders[bridgeName].ignitionFrame < 0){
                    unFinish = true;
                }
                if (!unFinish){
                    // all fired and burnout, so stop capturing
                    isEndingCapturing = true;
                    FrameNumber = _this_frame - IgniteTime;
                }
            }
            else if (isEndingCapturing) {
                if (FrameNumber + IgniteTime + cfg.WaitTime == _this_frame){   
                    _maincamera.SetActive(false);
                    GameObject.Find(bridgeName).GetComponent<FlammableObject>().fireCrawlSpeed = cfg.bridge_fireCrawlSpeed;
                    FireRecorder.onCapture = false;
                    iscapturing = false;
                }
            }
        }
    }

    public void CheckToStopSimulation(){
        if (isignited){
            if (_this_frame == SetDropTime + cfg.MaxDropTime){
                // check if all fire is ignited
                AllFireRecorders = new();
                foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
                    if (tag.name == cfg.trigger_name) continue;
                    AllFireRecorders.Add(tag.name, tag);
                }
            }
            else if (_this_frame % 20 == 0 && _this_frame >= SetDropTime + cfg.BridgeCrawlTime) {
                bool unFinish = false; // isBurning = false;
                foreach ((string name, FireRecorder tag) in AllFireRecorders){
                    if (tag.ignitionFrame > 0 && tag.flammability){
                        foreach(string nei in tag.touchers){
                            if (nei.Contains("Floor")||nei.Contains("trigger")) continue;
                            if (AllFireRecorders[nei].burnoutFrame == -1 && AllFireRecorders[nei].flammability){
                                unFinish = true;
                                break;
                            }
                        }
                        if (unFinish) break;
                    }
                }
                if (!unFinish){
                    // all fired and burnout, so stop sim
                    StopThisIterImmediatelyAndInvalidIt(true);
                }
            }
        }
    }

    public bool AllStatic(){
        bool allStatic = true;
        foreach (FireRecorder tag in tagManager.Query<FireRecorder>()){
            if (tag.gameObject.name == cfg.trigger_name) continue;
            if (!tag.isStatic){
                allStatic = false;
                break;
            }
        }
        return allStatic;
    } 

    public void StateCapture(){
        objPos = new ();
        objRot = new ();
        objScale = new ();
        objRelation = new ();
        GameObject go;
        Vector3 tmp;
        foreach(FireRecorder fo in GameObject.FindObjectsOfType<FireRecorder>()){
            go = fo.gameObject;
            tmp = go.transform.position;
            objPos.Add(     go.name, new List<float>(){tmp.x, tmp.y, tmp.z});
            tmp = go.transform.rotation.eulerAngles;
            objRot.Add(     go.name, new List<float>(){tmp.x, tmp.y, tmp.z});
            tmp = go.transform.localScale;
            objScale.Add(   go.name, new List<float>(){tmp.x, tmp.y, tmp.z});
            objRelation.Add(go.name, fo.touchers);
        }
    }

    protected override void OnAwake() {
        base.OnAwake();
    }

    protected override void OnScenarioStart() {
        base.OnScenarioStart();
        // Build Scene
        GameObject.DestroyImmediate(GameObject.Find("TestEnvironment"));
        GameObject env = AddObjectFromPrefab(cfg.room_prefab_path, cfg.room_name, cfg.room_pos, cfg.room_rotation);
        floor = env;
        manualDeleteImages = true;
        // add Environment scripts
        AddObjectFromPrefab(cfg.flame_engine_path, cfg.flame_engine_path);
        AddObjectFromPrefab(cfg.postprocess_path,  cfg.postprocess_path );
        AddObjectFromPrefab(cfg.reflecprob_path,   cfg.reflecprob_path  );
        AddObjectFromPrefab(cfg.dirlight_path,     cfg.dirlight_path    , cfg.light_pos, cfg.light_rotation);
        _ObjManager.Remove(cfg.room_name);
        _ObjManager.Remove(cfg.flame_engine_path);
        _ObjManager.Remove(cfg.postprocess_path );
        _ObjManager.Remove(cfg.reflecprob_path  );
        _ObjManager.Remove(cfg.dirlight_path    );
    }

    protected override void OnIterationStart()
    {
        base.OnIterationStart();
        done = false;
        isignited = false;
        FireRecorder.onCapture = false;
        _scenario.framesPerIteration = cfg.framesPerIteration;
        _pre_name = cfg.scene_name;
        // Build Scene
        BuildIterScene();
    }
    
    int SetDropTime; 
    protected override void OnUpdate() {
        base.OnUpdate();
        // Output Annotations
        if ( _this_iter_ft ){
            CheckStartIgnition(); // Also Start Capturing
            if (isignited && _this_frame == SetDropTime) SetIgnition();
            CheckToStopCapture();
            CheckToStopSimulation();
        }
        else {
            // counterfactual mdoe
            if (!isignited && _this_frame == SetDropTime){
                if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_CHANGETRIGGER){
                    triggerCounterfact = triggerCandidates[_this_cf_iter - this_unable];
                    SetIgnition(true);
                }
                else {
                    SetIgnition(false);
                }
                isignited = true;
            }
            CheckToStopSimulation();
        }

    }

    protected void setCamera(){
        CameraRandomizer cr = _Randomizers["CameraRandomizer"] as CameraRandomizer;
        cr.SampleCamera();
        FlammableObject[] gos = GameObject.FindObjectsOfType<FlammableObject>();
        List<GameObject> All = new ();
        foreach(FlammableObject go in gos){
            All.Add(go.gameObject);
        }
        cr.EncapsulateObjects(
            All,
            _maincamera.GetComponent<Camera>(),
            padding:cfg.field_paddings
        );
        Vector3 pos_now = _maincamera.transform.position;
        float normal = Math.Abs(pos_now.magnitude);
        cr.MoveCameraPosition(pos_now / normal * cfg.CamFinalDist, _maincamera.GetComponent<Camera>());
    }

    protected override void OnIterationEnd(){
        base.OnIterationEnd();
    }

    protected override List<List<float>> GetSoftbodyState(GameObject go){
        List<List<float>> _positions1 = new List<List<float>>();
        var tag = go.GetComponent<FireRecorder>();
        if (tag != null) {
            // this input is a Wood
            // add fire particles
            string name = go.name;
            int id = tag.perFrameParticleVec.Count-1;
            if (id >= 0){
                _positions1.AddRange(tag.perFrameParticleVec[id]);
            }
        }
        else {
            throw new Exception("Hand Wrote Error: FireRecorder have not attached to this Softbody: " + go.name);
        }
        return _positions1;
    }

    protected override (List<List<float>>, List<int>) GetSoftbodyGeometry(GameObject go){
        var temp = base.GetSoftbodyGeometry(go);
        var tag = go.GetComponent<FireRecorder>();
        if (tag != null){
        }
        return temp;
    }


    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        if (!validity) { 
            return temp; 
        }                
        GetFireEventTimes();
        GetFireRelations();
        // mode
        temp.Add("mode", "");
        // properties
        temp.Add("fireCrawlSpeed", fireCrawlSpeed);
        temp.Add("durationTime", durationTime);
        temp.Add("flammability", flammability);
        // saving states
        temp.Add("fireIgniteRelations", fireIgniteRelations);
        temp.Add("resultStartIgnitionTime", startIgnitionTime);
        temp.Add("resultEndIgnitionTime", endIgnitionTime);
        if (cfg.ANNO_MODE == cfg.FACTUAL){
            temp["mode"] = "FACTUAL_PREDICTIVE";
            temp.Add("resultWoodPos", objPos);
            temp.Add("resultWoodRot", objRot);
            temp.Add("resultWoodScale", objScale);
            temp.Add("leftFlammableGroups", flammableGroups);
            temp.Add("rightFlammableGroups", rightFlammableGroups);
            temp.Add("touchGraph", objRelation);
            temp.Add("captureSteps", FrameNumber);
            temp.Add("captureStartStep", IgniteTime);
            temp.Add("triggerStartStep", SetDropTime);
            // meta data saving
            Dictionary<string, object> temp_meta = new();
            temp_meta.Add("leftWoodData", woodData);
            temp_meta.Add("rightWoodData", woodPredData);
            temp_meta.Add("bridgeData", bridgeData);
            temp_meta.Add("bridgeName", bridgeName);
            temp_meta.Add("triggerFirstCollisionPoint", ignitionTriggerXYZ);
            temp_meta.Add("triggerName", cfg.trigger_name);
            temp.Add("metaSamplingData", temp_meta);
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_UNABLE){
            temp["mode"] = "COUNTERFACTUAL_unable_flammability";
            temp.Add("targetUnableObject", UnableWoodList[_this_cf_iter]);
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_CHANGETRIGGER){
            temp["mode"] = "COUNTERFACTUAL_change_trigger_origin";
            temp.Add("targetChangeTrigger", triggerCounterfact.Item1);
            temp.Add("targetChangeTriggerInitPosition", new List<float>(){triggerCounterfact.Item2, 
                                                                            triggerCounterfact.Item3,
                                                                            triggerCounterfact.Item4});
        }
        return temp;
    }

    protected override void OnCounterFactualDone(){
        cfg.ANNO_MODE = cfg.FACTUAL;
    }

    protected override void OnValidFactualDone(){
        this_unable = UnableWoodList.Count;
        this_trigger = triggerCandidates.Count;
        _cf_iter_total = this_trigger + this_unable;
    }
    
    protected override void OnCounterFactualIterStart(){
        if (_this_cf_iter == this_unable){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL_CHANGETRIGGER;
        }
        else if(_this_cf_iter == 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL_UNABLE;
        }
    }

}
