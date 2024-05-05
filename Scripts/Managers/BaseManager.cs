using System;
using System.Collections.Generic;
using Unity.VisualScripting;

using System.IO;
using Newtonsoft.Json;
using UnityEngine.Perception.GroundTruth.LabelManagement;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.Perception.GroundTruth.Labelers;
using UnityEngine.Perception.GroundTruth;
using PRUtils;
using PlacementConfig;
using MVoxelizer;
using UnityMeshSimplifier;
// NOTE: Counterfactual: only start and end functions are different.

namespace UnityEngine.Perception.Randomization.Randomizers{

// Base class for other scenarios. Handling Labeling and Randomization
public class BaseManager : BaseRandomizer
{
    protected string _pre_name; 
    protected string _target_dir="";
    protected int _data_id;
    protected List<float> time_seq = new List<float>();
    protected int _substeps;
    protected int iter_real = 0;
    protected int _valid_iter_num;
    protected int _total_valid_iter=5;
    protected int _this_iter; 
    protected int _framerate;
    protected bool output_vid_only = true;
    bool output_bbox=false, output_objCount=false, output_objInfo=false, output_ss=false, output_is=false, output_norm=false, output_depth=false;
    protected MainDataGeneration _scenario;
    protected GameObject _maincamera;
    protected Dictionary<string, GameObject> _ObjManager = new Dictionary<string, GameObject>();
    protected Dictionary<string, bool>                    _RigidbodySaveManageStaticity = new (); // objs->frame->particles->vector3
    protected Dictionary<string, GameObject>              _RigidbodySaveManage          = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<List<List<float>>>> _RigidbodySaveCache           = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<List<float>>>       _RigidbodyParticleSaveCache   = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<List<float>>>       _RigidbodyMeshVSaveCache      = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<int>>               _RigidbodyMeshFSaveCache      = new (); // objs->frame->particles->vector3
    protected Dictionary<string, GameObject>              _SoftbodySaveManage           = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<List<List<float>>>> _SoftbodySaveCache            = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<List<List<float>>>> _SoftbodyMeshVSaveCache       = new (); // objs->frame->particles->vector3
    protected Dictionary<string, List<List<int>>>         _SoftbodyMeshFSaveCache       = new (); // objs->frame->particles->vector3
    protected Dictionary<string, BaseRandomizer> _Randomizers = new Dictionary<string, BaseRandomizer>();  // Sampling and randoming, Saving the sampled properties
    protected Dictionary<string, CameraLabeler> _Labelers = new Dictionary<string, CameraLabeler>();
    public bool validity;
    public int validTrialNum;   

    // cfmode settings
    protected bool cFMode = false;
    protected bool _this_iter_ft = true; 
    protected int _this_cf_iter = 0, _cf_iter_total = 0;
    Dictionary<string, object> output, output_cf;

    protected float voxel_absolute_size;
    protected bool realtime_voxelize = false;
    public override void SaveAll(string path_without_slash){
        // pass
    }

    protected void AddStateWriter(string st, GameObject go, bool is_softbody=false, bool is_static=false){
        // User Warning: objects should be re-added each iter.  
        if (is_softbody){
            if (!_SoftbodySaveManage.ContainsKey(st)) {
                _SoftbodySaveManage           .Add(st, go);
                _SoftbodySaveCache            .Add(st, new());
                _SoftbodyMeshVSaveCache       .Add(st, new());
                _SoftbodyMeshFSaveCache       .Add(st, new());
            }
        }
        else {
            if (!_RigidbodySaveManage.ContainsKey(st)) {
                _RigidbodySaveManageStaticity .Add(st, is_static);
                _RigidbodySaveManage          .Add(st, go);
                _RigidbodySaveCache           .Add(st, new());
                _RigidbodyParticleSaveCache   .Add(st, null);
                _RigidbodyMeshVSaveCache      .Add(st, new());
                _RigidbodyMeshFSaveCache      .Add(st, new());
            }
        }
    }
    
    protected List<List<float>> GetRigidbodyVoxelsPosition(GameObject go){
        List<List<float>> cache = new();
        Transform voxelt = go.transform.Find("voxel");
        if ((!realtime_voxelize) && voxelt == null) throw new Exception("Hand Wrote Error: No <voxel> child found for rigidbody: " + go.name);
        else if (voxelt != null){ // use preset object voxels
            GameObject parent = voxelt.gameObject;
            int children = parent.transform.childCount;
            for (int i=0; i<children; i++) {
                Transform child = parent.transform.GetChild(i);
                cache.Add(Vec2List(child.transform.position));
            }
        }
        else { // real time generate voxels
            MVoxelizer.MeshVoxelizer a = new();
            a.sourceGameObject = go;
            a.generationType = GenerationType.SeparateVoxels;
            a.voxelSizeType = VoxelSizeType.AbsoluteSize;
            a.absoluteVoxelSize = voxel_absolute_size;
            a.ignoreScaling = true;
            a.uvConversion = UVConversion.None;
            a.fillCenter = FillCenterMethod.ScanlineZAxis;
            GameObject outVoxels = a.VoxelizeMesh();
            for (int i=0; i<outVoxels.transform.childCount; i++) {
                Transform child = outVoxels.transform.GetChild(i);
                cache.Add(Vec2List(child.transform.position));
            }
            GameObject.DestroyImmediate(outVoxels);
        }
        return cache;
    }
    
    protected GameObject AddObjectFromPrefab(string prefab_path_under_Resource, string name, Vector3 position=new Vector3(), Quaternion rotation=new Quaternion()){
        GameObject go = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(prefab_path_under_Resource), position, rotation);
        go.name = name;
        _ObjManager.Add(name, go);
        return go;
    }  

    protected GameObject AddObject(GameObject go){
        _ObjManager.Add(go.name, go);
        return go;
    }  
    
    protected PerceptionCamera AddPcptCamWithLabeler(Type labeler, string LabelConfigPathUnderRsrc)
    { 
        // Warn: Configs and Prefabs should be completely labeled before this...
        PerceptionCamera percam = _maincamera.GetComponent(typeof(PerceptionCamera)) as PerceptionCamera; 
        _LabelConfigPathUnderRsrc = LabelConfigPathUnderRsrc;
        if (labeler == typeof(DepthLabeler)){
            percam.AddLabeler(new DepthLabeler());
            output_depth = true;
        }
        else if (labeler == typeof(NormalLabeler)){
            percam.AddLabeler(new NormalLabeler());
            output_norm = true;
        }
        else if (labeler == typeof(SemanticSegmentationLabeler)){
            SemanticSegmentationLabeler lb = new SemanticSegmentationLabeler();
            lb.labelConfig = Resources.Load(LabelConfigPathUnderRsrc) as SemanticSegmentationLabelConfig;
            percam.AddLabeler(lb);
            output_ss = true;
        }
        else if (labeler == typeof(InstanceSegmentationLabeler)){
            InstanceSegmentationLabeler lb = new InstanceSegmentationLabeler();
            lb.idLabelConfig = Resources.Load(LabelConfigPathUnderRsrc) as IdLabelConfig;
            percam.AddLabeler(lb);
            isl = lb;
            output_is = true;
        }
        else if (labeler == typeof(BoundingBox2DLabeler)){
            BoundingBox2DLabeler lb = new BoundingBox2DLabeler();
            lb.idLabelConfig = Resources.Load(LabelConfigPathUnderRsrc) as IdLabelConfig;
            percam.AddLabeler(lb);
            output_bbox = true;
        }
        else if (labeler == typeof(ObjectCountLabeler)){
            ObjectCountLabeler lb = new ObjectCountLabeler();
            lb.labelConfig = Resources.Load(LabelConfigPathUnderRsrc) as IdLabelConfig;
            percam.AddLabeler(lb);
            output_objCount = true;
        }
        else if (labeler == typeof(RenderedObjectInfoLabeler)){
            RenderedObjectInfoLabeler lb = new RenderedObjectInfoLabeler();
            lb.idLabelConfig = Resources.Load(LabelConfigPathUnderRsrc) as IdLabelConfig;
            percam.AddLabeler(lb);
            output_objInfo = true;
        }
        else{
            Debug.Log("Error: Undefined Labeler");
        }
        return percam;
    }
    InstanceSegmentationLabeler isl;
    string _LabelConfigPathUnderRsrc;
    protected void RefreshInsSegLabeler(){
        if(output_is){
            PerceptionCamera percam = _maincamera.GetComponent(typeof(PerceptionCamera)) as PerceptionCamera; 
            percam.RemoveLabeler(isl);
            isl = new InstanceSegmentationLabeler();
            isl.idLabelConfig = Resources.Load(_LabelConfigPathUnderRsrc) as IdLabelConfig;
            percam.AddLabeler(isl);
        }
    }

    protected void AddRandomizerAtLast(string name, BaseRandomizer rndmzr){
        _scenario.AddRandomizer(rndmzr);
        _Randomizers.Add(name, rndmzr);
    }

    protected void AddRandomizerAtIndex(string name, BaseRandomizer rndmzr, int idx){
        _scenario.InsertRandomizer(idx, rndmzr);
        _Randomizers.Add(name, rndmzr);
    }
    
    protected UnityEngine.Component AddRandTagToObject(GameObject ls, Type rndtagtype){
        UnityEngine.Component c = ls.GetComponent(rndtagtype);
        if (c == null) {
            c = ls.AddComponent(rndtagtype);
        }
        return c;
    }
    
    protected void Add_NameList_ToRandomizer(List<string> ls, Type rndtagtype){
        foreach (string st in ls) {
            GameObject go = GameObject.Find(st);
            if (go != null)
                AddRandTagToObject(go, rndtagtype);
        }
    }

    protected bool Is_Both_Label_Configs_Exist_and_Not_Empty(string idLabelConfigPathUnderRsrc, string ssLabelConfigPathUnderRsrc)
    {
        IdLabelConfig ilc = Resources.Load(idLabelConfigPathUnderRsrc) as IdLabelConfig;
        SemanticSegmentationLabelConfig sslc = Resources.Load(ssLabelConfigPathUnderRsrc) as SemanticSegmentationLabelConfig;
        if (ilc != null && sslc != null && ilc.labelEntries.Count > 0 && sslc.labelEntries.Count > 0) return true;
        else return false;
    }
 
    // need/need not to be overrid in derived class 
    // NOTE: 0: state points tracked, 1: vertices sampled each frame, 2: faces sampled each frame
    protected virtual List<List<float>> GetRigidbodyState(GameObject go){
        var temp = new List<List<float>>();
        temp.Add(Vec2List(go.transform.position));
        temp.Add(Vec2List(go.transform.rotation.eulerAngles));
        return temp;
    }

    // need/need not to be overrid in derived class 
    // NOTE: 0: state points tracked, 1: vertices sampled each frame, 2: faces sampled each frame
    protected virtual (List<List<float>>, List<int>) GetRigidbodyGeometry(GameObject go){
        var temp = new List<List<float>>();
        var temp_f = new List<int>();
        var m = go.GetComponent<MeshFilter>().sharedMesh;
        var vs = m.vertices;
        var fs = m.triangles;
        for (int i=0; i < vs.Length; i++){
            temp.Add(Vec2List(go.transform.TransformPoint(vs[i])));
        }
        temp_f.AddRange(fs);
        return (temp, temp_f);
    }

    // need to be overrid in derived class 
    // NOTE: 0: state points tracked, 1: vertices sampled each frame, 2: faces sampled each frame
    protected virtual List<List<float>> GetSoftbodyState(GameObject go){
        var temp = new List<List<float>>();
        return temp;
    }

    protected virtual (List<List<float>>, List<int>) GetSoftbodyGeometry(GameObject go){
        var temp = new List<List<float>>();
        var temp_f = new List<int>();
        return (temp, temp_f);
    }
    
    // need to be overrid in derived class: build scheme on scenario start 
    public virtual void BuildScheme(){}
    
    // need to be overrid in derived class: build each iter scene
    protected virtual void BuildIterScene(){}

    protected override void OnAwake() {
        _substeps = 8;
        // _maincamera = GameObject.Find("Main Camera");
        // _scenario = GameObject.Find("Simulation").GetComponent(typeof(UnityEngine.Perception.Randomization.Scenarios.FixedLengthScenario)) as UnityEngine.Perception.Randomization.Scenarios.FixedLengthScenario;
        // _Randomizers = new Dictionary<string, BaseRandomizer>();
        // _Labelers = new Dictionary<string, CameraLabeler>();
        // Add Perception Camera
        PerceptionCamera percam = _maincamera.GetComponent(typeof(PerceptionCamera)) as PerceptionCamera;
        if (percam == null){
            percam = _maincamera.AddComponent(typeof(PerceptionCamera)) as PerceptionCamera;
        }
    }

    protected override void OnScenarioStart() {
        // Warning: Start() do not implement before IterStart
        iter_real = 0;
        _this_iter = 0;
        _valid_iter_num = 0;
        // _total_frames = _scenario.framesPerIteration;
        RunProgram.DeleteFolder("./output/images/solo");
    }

    protected void RefreshCacheEachIter(){
        _RigidbodySaveManageStaticity = new ();
        _RigidbodySaveManage          = new ();
        _RigidbodySaveCache           = new ();
        _RigidbodyParticleSaveCache   = new ();
        _RigidbodyMeshVSaveCache      = new ();
        _RigidbodyMeshFSaveCache      = new ();
        _SoftbodySaveManage           = new ();
        _SoftbodySaveCache            = new ();
        _SoftbodyMeshVSaveCache       = new ();
        _SoftbodyMeshFSaveCache       = new ();
    }

    protected override void OnIterationStart()
    {
        if (!cFMode || (cFMode && _this_iter_ft)){
            // Application.targetFrameRate = 150; 
            // _scenario.framesPerIteration = _total_frames;
            _data_id = _this_iter; _this_iter++;
        }
        else {
            OnCounterFactualIterStart();
        }
        _pre_name = "spinning";
        _this_frame = 0;
        validity = true;
        // Destroy Objects
        foreach ((string key, GameObject go) in _ObjManager){ GameObject.DestroyImmediate(go); }
        time_seq = new List<float>();
        _ObjManager = new Dictionary<string, GameObject>();
        RefreshCacheEachIter();
    }

    protected override void OnUpdate() {
        float thistime = new float();
        thistime = Time.time;
        _this_frame++;
        // Output Annotations
        time_seq.Add(thistime);
        UpdateStateCache();
    }

    protected virtual void UpdateStateCache(){
        var factual_mode = (!cFMode) || (cFMode && _this_iter_ft);
        // 30fps for state logging
        if (factual_mode && _this_frame % GlobalSettings.particleCaptureRate == 0 && _maincamera.activeSelf) {
            // update rigidbodies
            foreach ((string key, GameObject go) in _RigidbodySaveManage){
                if (!_RigidbodySaveManageStaticity[key]){
                    _RigidbodySaveCache[key].Add(GetRigidbodyState(go));
                }
                if ( _RigidbodyParticleSaveCache[key] == null){ // only once for space efficiency
                    // particles
                    _RigidbodyParticleSaveCache[key] = new();
                    var temp = GetRigidbodyState(go);
                    _RigidbodyParticleSaveCache[key].Add(temp[0]);
                    _RigidbodyParticleSaveCache[key].Add(temp[1]);
                    _RigidbodyParticleSaveCache[key].AddRange(GetRigidbodyVoxelsPosition(go));
                    // meshes
                    var temp_all = GetRigidbodyGeometry(go);
                    _RigidbodyMeshVSaveCache[key] = temp_all.Item1;
                    _RigidbodyMeshFSaveCache[key] = temp_all.Item2;
                }
            }
            foreach ((string key, GameObject go) in _SoftbodySaveManage){
                // particles
                _SoftbodySaveCache[key].Add(GetSoftbodyState(go));
                // meshes
                var temp_all = GetSoftbodyGeometry(go);
                _SoftbodyMeshVSaveCache[key].Add(temp_all.Item1);
                _SoftbodyMeshFSaveCache[key].Add(temp_all.Item2);
            }
        }
    }

    protected void convert2Video(string input_format, string output_format, int start_number=-1, int end_number=-1, 
                                bool rename=false, string ffmpeg_path = "ffmpeg", 
                                string insSegFormat="" /*, int total_frames = -1*/) {
        string[] splitString = output_format.Split('/');
        string lastSubstring = splitString[splitString.Length - 1];
        if(_maincamera.GetComponent<PerceptionCamera>().captureRgbImages)
        {   
            if(rename){
                char[] divider = new char[]{'/'};
                string[] splits = input_format.Split(divider);
                string input_dir = input_format.Clone().ToString();
                input_dir.Replace("/"+splits[splits.Length-1], "");
                string cmd = "src_path=\""+input_dir+"\"; "
                +"start_num=$(ls \"$src_path\"/step*.camera.png | grep -oP '(?<=step)\\d+(?=.camera.png)' | sort -n | head -1);"
                +"for file in \"$src_path\"/step*.camera.png; "
                +"do num=$(basename \"$file\" | grep -oP '(?<=step)\\d+(?=.camera.png)'); "
                +"new_num=$((num - start_num)); "
                +"mv \"$file\" \"$src_path/step${new_num}.camera.png\"; "
                +"done";
                RunProgram.Main(cmd);
            }
            
            if (start_number == -1 && end_number == -1)
                RunProgram.Main(ffmpeg_path + " "
                                + " -framerate " + _framerate.ToString() 
                                + " -i " + input_format 
                                + " -c:v libx264 -profile:v high -crf 20 -pix_fmt yuv420p " 
                                + output_format);
            else{
                RunProgram.Main(ffmpeg_path + " "
                                + " -start_number " + start_number.ToString()
                                + " -to 00:00:" +( (float)(end_number - start_number) / _framerate).ToString()
                                + " -framerate " + _framerate.ToString() 
                                + " -i " + input_format 
                                + " -c:v libx264 -profile:v high -crf 20 -pix_fmt yuv420p " 
                                + output_format);
            }
        }   
            
        if (output_is){
            if(rename){
                char[] divider = new char[]{'/'};
                string[] splits = input_format.Split(divider);
                string input_dir = input_format.Clone().ToString();
                input_dir.Replace("/"+splits[splits.Length-1], "");
                string cmd = "src_path=\""+input_dir+"\"; "
                +"start_num=$(ls \"$src_path\"/step*.camera.instance\\ segmentation.png | grep -oP '(?<=step)\\d+(?=.camera.instance\\ segmentation.png)' | sort -n | head -1);"
                +"for file in \"$src_path\"/step*.camera.instance\\ segmentation.png; "
                +"do num=$(basename \"$file\" | grep -oP '(?<=step)\\d+(?=.camera.instance\\ segmentation.png)'); "
                +"new_num=$((num - start_num)); "
                +"mv \"$file\" \"$src_path/step${new_num}.camera.instance\\ segmentation.png\"; "
                +"done";
                RunProgram.Main(cmd);
            }
            string seg_format = insSegFormat == ""? input_format.Substring(0, input_format.Length - 4) + ".instance\\ segmentation.png" : insSegFormat;
            
            if (start_number == -1 && end_number == -1)
                RunProgram.Main(ffmpeg_path + " "
                                + " -framerate " + _framerate.ToString() 
                                + " -i " + seg_format
                                + " -c:v libx264 -profile:v high -crf 20 -pix_fmt yuv420p " 
                                + output_format.Substring(0, output_format.Length - 4) 
                                + "_seg.mp4");
            else
                RunProgram.Main(ffmpeg_path + " "
                                + " -start_number " + start_number.ToString()
                                + " -to 00:00:" +( (float)(end_number - start_number) / _framerate).ToString()
                                + " -framerate " + _framerate.ToString() 
                                + " -i " + seg_format
                                + " -c:v libx264 -profile:v high -crf 20 -pix_fmt yuv420p " 
                                + output_format.Substring(0, output_format.Length - 4) 
                                + "_seg.mp4");
        }
        
    }

    protected List<string> allInOne_path_queue = new(), solve_output_target_dir = new();
    protected List<Dictionary<string, object>> output_queue = new();
    
    protected void solveOutput(){
        // solve last trial annotations
        if (allInOne_path_queue.Count > 0){
            if (allInOne_path_queue.Count > 1) throw new System.Exception("allInOne_path_queue's Count should be in {0, 1}");
            string input_path_wodash = allInOne_path_queue[0];
            Dictionary<string, object> bboxes = new();
            bool done = false; 
            for (int i = 0; ; i++){
                string jsonFilePath = input_path_wodash + "/step" + i.ToString() + ".frame_data.json";
                if (!File.Exists(jsonFilePath)) break;
                Dictionary<string, object> dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(jsonFilePath));
                if (dictionary.ContainsKey("captures")) {
                    done = true;
                    bboxes.Add(i.ToString(), dictionary["captures"]);
                }
                else{
                    if (done){
                        Debug.Log("No captures in " + jsonFilePath);
                        break;
                    }
                }
            }
            Dictionary<string, object> output_last = output_queue[0];
            (output_last["imgAnnotationDescriptions"] as Dictionary<string, object>)["perFrameAnnotations"] = bboxes;
            Dictionary<string, object> output_final = new (), output_final_pc = new ();
            foreach (var item in output_last){
                if (item.Key == "softbody4D" || item.Key == "rigidbody4D")
                    output_final_pc.Add(item.Key, item.Value);
                else
                    output_final.Add(item.Key, item.Value);
            }
            File.WriteAllText(solve_output_target_dir[0] + "/" + "outputs.json", JsonConvert.SerializeObject(output_final));
            File.WriteAllText(solve_output_target_dir[0] + "/" + "outputs4D.json", JsonConvert.SerializeObject(output_final_pc));
            allInOne_path_queue.RemoveAt(0);
            solve_output_target_dir.RemoveAt(0);
            output_queue.RemoveAt(0);
        }
    }

    protected void addQueue(string input_path_wodash_this_trial, string target_dir_this_trial, Dictionary<string, object> output_this_trial){
        allInOne_path_queue.Add(input_path_wodash_this_trial);
        solve_output_target_dir.Add(target_dir_this_trial);
        output_queue.Add(output_this_trial);
    }

    protected Dictionary<string, object> prepareId2ObjectNameDict(){
        Dictionary<string, object> temp2 = new();
        temp2.Add("imgAnnotationDescriptions", new Dictionary<string, object>());
        Dictionary<string, object> temp21 = (temp2["imgAnnotationDescriptions"] as Dictionary<string, object>);
        temp21.Add("perFrameAnnotations", new Dictionary<string, object>());
        temp21.Add("Id2ObjectNameDict", new Dictionary<uint, string>());
        Dictionary<uint, string> temp_id2nm = (temp21["Id2ObjectNameDict"] as Dictionary<uint, string>);
        Labeling[] objects = GameObject.FindObjectsOfType<Labeling>();
        for (int i = 0; i < objects.Length; i++){
            Labeling go = objects[i];
            temp_id2nm.Add(go.instanceId, go.name);
        }
        return temp2;
    }

    protected bool manualDeleteImages = false;
    List<List<object>> vidTaskQueue = new();
    List<string> todeleteFolder = new();
    protected int start_image_id; // image id, not the on-update id
    protected int end_image_id; // image id, not the on-update id
    protected virtual void VideoSavingLogic(){
        if (vidTaskQueue.Count > 0){
            List<object> task = vidTaskQueue[0];
            vidTaskQueue.RemoveAt(0);
            string srcPath0 = task[0] as string;
            string targetPath = task[1] as string;
            int start_time = (int)task[2];
            int end_time = (int)task[3];
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT){
                bool boolvalue = (bool)task[4];
                string ffmpeg_path = task[5] as string;
                string mask_afterfix = task[6] as string;
                convert2Video(srcPath0, targetPath, start_time, end_time, boolvalue, ffmpeg_path, mask_afterfix);
            }
            else {
                convert2Video(srcPath0, targetPath, start_time, end_time);
            }
            
            // delete the folder
            string tode = todeleteFolder[0];
            while (Directory.Exists(tode)){RunProgram.DeleteFolder(tode);}
            todeleteFolder.RemoveAt(0);
        }
        List<object> next = new();
        next.Add("./output/images/solo/sequence." + iter_real.ToString() + "/step%d.camera.png");
        next.Add(_target_dir + "/" + _data_id.ToString() + "/output_Full.mp4");
        next.Add(start_image_id);
        next.Add(end_image_id);
        if (System.Environment.OSVersion.Platform == PlatformID.Win32NT){
            next.Add(false);
            next.Add(SystemSettings.ffmpeg_path);
            next.Add("\"./output/images/solo/sequence." + iter_real.ToString() + "/step%d.camera.instance segmentation.png\"");
        }
        vidTaskQueue.Add(next);
        todeleteFolder.Add("./output/images/solo/sequence." + iter_real.ToString());
    }
    
    protected virtual void OnCounterFactualDone(){
        // Do nothing
    }

    protected virtual void OnValidFactualDone(){
        // Do nothing
    }

    protected virtual void OnCounterFactualIterStart(){
        // Do nothing
    }

    protected override void OnIterationEnd(){
        if (!cFMode || (cFMode && _this_iter_ft)){
            // not cf mode or first iter in cf mode(factual data output)
            // Last frame: Save all data
            if (_target_dir == "") { 
                Directory.CreateDirectory("./output");
                _target_dir = "./output/" + _pre_name;
                string directoryPath = _target_dir;
                int count = 0;
                while (Directory.Exists(directoryPath)) {
                    count++;
                    directoryPath = _target_dir + "_" + count.ToString();
                }
                Directory.CreateDirectory(directoryPath);
                _target_dir = directoryPath;
            }
            string target_dir = _target_dir + "/" + _data_id.ToString();
            Directory.CreateDirectory(target_dir);
            
            output = new();
            string srcPath = "./output/images/solo/sequence." + iter_real.ToString();
            output.Add("validity", validity);
            // Save Sampled Parameters
            if (!cFMode){
                // Save Sampled Parameters
                foreach ((string st, BaseRandomizer br) in _Randomizers) { output.AddRange(br.GetAll()); }
                // Save Multiple Annotation States
                output.AddRange(prepareId2ObjectNameDict());
                Dictionary<string, object> particlesRG = new(), particlesSF = new();
                particlesRG.Add("rigidbodyStatesStaticity", _RigidbodySaveManageStaticity);
                particlesRG.Add("rigidbodyCentroidStates" , _RigidbodySaveCache);
                particlesRG.Add("rigidbodyMeshVertices"   , _RigidbodyMeshVSaveCache);
                particlesRG.Add("rigidbodyMeshFaces"      , _RigidbodyMeshFSaveCache);
                particlesRG.Add("rigidbodyVoxelPosition"  , _RigidbodyParticleSaveCache);
                particlesSF.Add("softbodyTrackedParticles", _SoftbodySaveCache);
                particlesSF.Add("softbodyMeshVertices"    , _SoftbodyMeshVSaveCache);
                particlesSF.Add("softbodyMeshFaces"       , _SoftbodyMeshFSaveCache);
                output.Add("rigidbody4D", particlesRG);
                output.Add("softbody4D", particlesSF);
                solveOutput();
                VideoSavingLogic();
                addQueue(srcPath, target_dir, output);
                Debug.Log(target_dir + " Saved. Iter" + _this_iter.ToString() + " Done.");
                if (!manualDeleteImages){while (Directory.Exists("./output/images/solo")){RunProgram.DeleteFolder("./output/images/solo");}}
            }
            else {
                if (validity){
                    // Save Sampled Parameters
                    foreach ((string st, BaseRandomizer br) in _Randomizers) { output.AddRange(br.GetAll()); }
                    // Save Multiple Annotation States
                    output.AddRange(prepareId2ObjectNameDict());
                    src_path_this_trial = srcPath;
                    Dictionary<string, object> particlesRG = new(), particlesSF = new();
                    particlesRG.Add("rigidbodyStatesStaticity", _RigidbodySaveManageStaticity);
                    particlesRG.Add("rigidbodyCentroidStates" , _RigidbodySaveCache);
                    particlesRG.Add("rigidbodyMeshVertices"   , _RigidbodyMeshVSaveCache);
                    particlesRG.Add("rigidbodyMeshFaces"      , _RigidbodyMeshFSaveCache);
                    particlesRG.Add("rigidbodyVoxelPosition"  , _RigidbodyParticleSaveCache);
                    particlesSF.Add("softbodyTrackedParticles", _SoftbodySaveCache);
                    particlesSF.Add("softbodyMeshVertices"    , _SoftbodyMeshVSaveCache);
                    particlesSF.Add("softbodyMeshFaces"       , _SoftbodyMeshFSaveCache);
                    output.Add("rigidbody4D", particlesRG);
                    output.Add("softbody4D", particlesSF);
                    solveOutput();
                    VideoSavingLogic(); 
                    // NOTE: this func should be called before the following output 
                    // (especially annotation masks otherwise the last several 
                    // frames cannot catch up with the spped of the thread)
                    OnValidFactualDone();
                    _this_iter_ft = false;
                    _maincamera.SetActive(false);
                    _this_cf_iter = 0;
                    _valid_iter_num += 1;
                    output_cf = new();
                    if (_cf_iter_total == 0){
                        // no cf waiting, continuing the factual mode
                        EndATrial();
                    }
                }
                else{
                    if (!manualDeleteImages){while (Directory.Exists("./output/images/solo")){RunProgram.DeleteFolder("./output/images/solo");}}
                    File.WriteAllText(target_dir + "/" + "outputs.json", JsonConvert.SerializeObject(output));
                    Debug.Log(target_dir + " Failed. Iter" + _this_iter.ToString() + " Done.");
                }
            }
        }
        else {
            // cache the data 
            Dictionary<string, object> output_cf0 = new();
            foreach ((string st, BaseRandomizer br) in _Randomizers) { output_cf0.AddRange(br.GetAll()); }
            output_cf.Add(_this_cf_iter.ToString(), output_cf0);
            _this_cf_iter++;
            if (_this_cf_iter == _cf_iter_total){
                // end of the counterfactual iters, saving the data
                EndATrial();
            }
        }
        iter_real++;
    }
    protected string src_path_this_trial;
    void EndATrial(){
        // end of the counterfactual iters, saving the data
        OnCounterFactualDone();
        _this_cf_iter = 0;
        _this_iter_ft = true;
        _maincamera.SetActive(true);

        string target_dir = _target_dir + "/" + _data_id.ToString();
        Directory.CreateDirectory(target_dir);
        output.Add("CounterFactualAnnotations", output_cf);
        addQueue(src_path_this_trial, target_dir, output);
        if (!manualDeleteImages){while (Directory.Exists("./output/images/solo")){RunProgram.DeleteFolder("./output/images/solo");}}
        Debug.Log(target_dir + " Saved. Iter" + _this_iter.ToString() + " Done.");
        if(_valid_iter_num >= _total_valid_iter){
            // quit the playing mode
            Debug.Log("All Done. Quitting...");
            _scenario.constants.iterationCount = _total_valid_iter;
        }
    }

    public static List<float> Vec2List(Vector3 vec){
        List<float> ls = new List<float>();
        ls.Add(vec.x); ls.Add(vec.y); ls.Add(vec.z);
        return ls;
    }

    protected Mesh Simplify(Mesh sourceMesh, float quality){
        // Create our mesh simplifier and setup our entire mesh in it
        var meshSimplifier = new MeshSimplifier();
        meshSimplifier.Initialize(sourceMesh);

        // This is where the magic happens, lets simplify!
        meshSimplifier.SimplifyMesh(quality);

        // Create our final mesh and apply it back to our mesh filter
        return meshSimplifier.ToMesh();
    }

    protected Mesh Standard2MeshFunc((List<List<float>>, List<int>) temp){
         // Create a new Mesh
        Mesh mesh0 = new Mesh();

        // Define vertices
        List<List<float>> verts = temp.Item1;
        int num = verts.Count;
        Vector3[] vertices = new Vector3[num];
        for (int i = 0; i < num; i++){
            vertices[i] = new Vector3(verts[i][0], verts[i][1], verts[i][2]);
        }
        List<int> faces = temp.Item2;
        int num_faces = faces.Count;
        int[] triangles = new int[num_faces];
        for (int i = 0; i < num_faces; i++){
            triangles[i] = faces[i];
        }
        // Assign vertices to the mesh
        mesh0.vertices = vertices;
        // Assign triangle indices to the mesh
        mesh0.triangles = triangles;

        // Recalculate normals for lighting (optional)
        mesh0.RecalculateNormals();
        return mesh0;
    }

    protected (List<List<float>>, List<int>) Mesh2StandardFunc(Mesh m, Transform t=null){
        var temp = new List<List<float>>();
        var temp_f = new List<int>();
        var vs = m.vertices;
        var fs = m.triangles;
        if (t == null){
            for (int i=0; i < vs.Length; i++){
                temp.Add(Vec2List(vs[i]));
            }
        }
        else {
            for (int i=0; i < vs.Length; i++){
                temp.Add(Vec2List(t.TransformPoint(vs[i])));
            }
        }
        temp_f.AddRange(fs);
        return (temp, temp_f);
    }

    GameObject DebugMesh;
    protected void DebugMeshFunc((List<List<float>>, List<int>) temp, Vector3 offset){
        var mesh0 = Standard2MeshFunc(temp);

        // Create a new GameObject and add a MeshFilter and MeshRenderer
        if (DebugMesh != null) GameObject.DestroyImmediate(DebugMesh);
        DebugMesh = new GameObject("DebugMesh");
        DebugMesh.transform.position += offset;
        MeshFilter meshFilter = DebugMesh.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = DebugMesh.AddComponent<MeshRenderer>();

        // Assign the created mesh to the MeshFilter
        meshFilter.mesh = mesh0;
    }

}

}
