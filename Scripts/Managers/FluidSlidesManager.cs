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
using FluidSlidesConfig;
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


public class FluidSlidesTag : RandomizerTag {
    public int capacity;
    public float rho;
    public float vis;
    public float sft;
    public int fluid_color;
    public bool visualize;
    public GameObject solver;
    public float pos_x, pos_y, pos_z;
    public float speed, randV;
    protected ObiFluidEmitterBlueprint blueprint;
    protected ObiEmitterShape shape;
    protected ObiEmitter emitter; 
    protected int _this_frame = 0;
    public HeatTransport HT;

    protected IEnumerator Setup_Fluid(int capacity=1000, 
                                float resolution=2,
                                float density=1000,
                                float smoothing=2,
                                float viscosty=0.02f,
                                float surfaceTension=0.5f,
                                float buoyancy=-1,
                                float atmosphericDrag=0,
                                float atmosphericPressure=0, 
                                float vorticity=0,
                                float diffusion=0)
    {
        blueprint = ScriptableObject.CreateInstance<ObiFluidEmitterBlueprint>();
        blueprint.capacity = (uint)capacity;
        blueprint.resolution = resolution;
        blueprint.restDensity = density;
        blueprint.smoothing = smoothing;
        blueprint.viscosity = viscosty;
        blueprint.surfaceTension = surfaceTension;
        blueprint.buoyancy = buoyancy;
        blueprint.atmosphericDrag = atmosphericDrag;
        blueprint.atmosphericPressure = atmosphericPressure;
        blueprint.vorticity = vorticity;
        blueprint.diffusion = diffusion;
        yield return blueprint.Generate();
    }
    private void Start() {
        StartCoroutine(Setup_Fluid(capacity:capacity, 
                                    resolution:cfg.fluid_resolution,
                                    density:rho,
                                    smoothing:cfg.fluid_smooth,
                                    viscosty:vis, 
                                    surfaceTension:sft,
                                    buoyancy:cfg.buoyancy,
                                    atmosphericDrag:cfg.atmosphericDrag,
                                    atmosphericPressure:cfg.atmosphericPressure,
                                    vorticity:cfg.vorticity,
                                    diffusion:cfg.diffusion));
        emitter = gameObject.GetComponent<ObiEmitter>();
        if (emitter == null) { 
            emitter = gameObject.AddComponent<ObiEmitter>();
        }
        shape = gameObject.AddComponent<ObiEmitterShapeEdge>();
        // assign initial burst shape 
        emitter.emissionMethod = ObiEmitter.EmissionMethod.STREAM;

        emitter.lifespan = cfg.fluid_emitter_lifespan;
        emitter.speed = 0;
        (shape as ObiEmitterShapeEdge).lenght = cfg.fluid_emitter_width;
        shape.Emitter = emitter;
        Color clr = SoftColor.color_rgb_dict[fluid_color];
        clr.a = cfg.fluid_color_apparency;
        gameObject.transform.position = new Vector3(pos_x, pos_y, pos_z);
        gameObject.transform.rotation = cfg.emitterRotation;
        gameObject.transform.localScale = cfg.emitterScale;
        emitter.emitterBlueprint = ScriptableObject.Instantiate(blueprint);
        emitter.transform.parent = solver.transform;
        emitter.speed = speed;
        emitter.randomVelocity = randV;
        if (visualize){
            var mc = gameObject.AddComponent<MakeCylinders>();
            mc.HT = HT;
            mc.solver = solver.GetComponent<ObiSolver>();
            mc.color = fluid_color;
        }
    }
    private void Update() {
    }
    public void modifyEmit(float speed){
        emitter.speed = speed;
    }
}



[Serializable]
[AddRandomizerMenu("Fluid Slides Manager")]
public class FluidSlidesManager : BaseManager
{
    public FluidSlidesSampler _sampler;
    private GameObject obisolver;
    PerceptionCamera pc_an;
    HeatTransport vft;
    Vector3 lookat = new Vector3(0, 0, 0);
    List<FluidSlidesTag> emittersForPred = new(), emittersForFactual = new();
    GameObject background;
    int this_cf_remove = 0;
    List<string> this_cf_remove_name_list;
    
    public override void BuildScheme()
    {
        // voxel output settings
        voxel_absolute_size = cfg.voxel_absolute_size;
        realtime_voxelize  = true;

        //video saving logic settings here
        start_image_id = 2;
        end_image_id = (int)(cfg.captureFrames / (1 + cfg.framesBetweenCaptures));

        cFMode = true;
        cfg.ANNO_MODE = cfg.FACTUAL;
        _total_valid_iter = cfg.ValidIterNum;
        _framerate = cfg.frameRate;
        UnityEngine.Random.InitState(cfg.seed);

        // init others
        string idconfig_path = "PerceptionConfigs/FluidSlidesIdLabelConfig";
        string ssconfig_path = "PerceptionConfigs/FluidSlidesSemanticSegmentationLabelConfig";
        if (Is_Both_Label_Configs_Exist_and_Not_Empty(idconfig_path, ssconfig_path))
        {
            // Init Neccessary Caches
            _scenario = GameObject.Find("Simulation").GetComponent(typeof(UnityEngine.Perception.Randomization.Scenarios.MainDataGeneration)) as UnityEngine.Perception.Randomization.Scenarios.MainDataGeneration;
            _maincamera = GameObject.Find("Main Camera");

            _sampler = new FluidSlidesSampler(cfg.seed);
            
            _scenario.framesPerIteration = cfg.framesPerIteration;
            _scenario.constants.randomSeed = (uint)cfg.seed;
            _scenario.constants.iterationCount = cfg.numIterations;
            _scenario.constants.startIteration = cfg.startIteration;
            // Init Randomizer
            FluidSlidesManager r1 = this;
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

    protected HeatTransport AddHeatTransportEffectOnSolver(GameObject obisolver, 
                                                    Dictionary<string, string> temp_dict, 
                                                    Dictionary<string, (Vector3, Vector3)> receptorCenterAndScale,
                                                    Dictionary<string, (double, double, double, double)> receptorGuidedMinXMaxXMinYMaxY,
                                                    Dictionary<string, float> fluidDensity){
        if (cfg.addViscosityEffect){ 
            // Add HeatTransport comp
            vft = obisolver.AddComponent<HeatTransport>();
            vft.Obj2Temp = temp_dict;
            vft.receptorCenterAndScale = receptorCenterAndScale;
            vft.receptorGuidedMinXMaxXMinYMaxY = receptorGuidedMinXMaxXMinYMaxY;
            vft.fluidDensity = fluidDensity;
            return vft;
        }
        return null;
    }

    protected void StartEmitters(List<FluidSlidesTag> emittersForPred){
        foreach (var emitter in emittersForPred){
            emitter.modifyEmit(cfg.fluid_emitter_speed);
        }
    }

    protected void SetObject_P3DMColor(GameObject go, int color){
        Material mat = go.GetComponent<MeshRenderer>().material;
        Color c = SoftColor.color_rgb_dict[color];
        mat.color = c;
        mat.SetColor("EmissiveColor", c);
    }

    protected void OpenEmitterGate(GameObject emitter){
        var gate = emitter.GetComponentInChildren<Gate>();
        gate.GetComponent<MeshRenderer>().enabled = false;
        gate.GetComponent<ObiCollider>().enabled = false;
        for(int i=0; i<emitter.transform.childCount; i++){
            var voxel = emitter.transform.GetChild(i);
            if (voxel.name == "voxel"){
                var gatevoxels = voxel.GetComponentsInChildren<GateVoxel>();
                foreach(var gv in gatevoxels){
                    gv.transform.position = new Vector3(0,0,0);
                }
                break;
            }
        }
    }
    float floor_y, ceiling_y, left_x, right_x; 
    protected void SetObjects(int toRemove=-1){
        // Build Container
        string container_vert1_name = "Left Vertical Wall";
        string container_vert2_name = "Right Vertical Wall";
        string container_hori1_name = "Floor";
        string container_hori2_name = "Ceiling";
        GameObject container_vert1 = AddObjectFromPrefab(cfg.wall_vert_prefab_path, container_vert1_name, new Vector3(0, 0, 0), Quaternion.identity);
        GameObject container_vert2 = AddObjectFromPrefab(cfg.wall_vert_prefab_path, container_vert2_name, new Vector3(0, 0, 0), Quaternion.identity);
        GameObject container_hori1 = AddObjectFromPrefab(cfg.wall_hori_prefab_path, container_hori1_name, new Vector3(0, 0, 0), Quaternion.identity);
        GameObject container_hori2 = AddObjectFromPrefab(cfg.wall_hori_prefab_path, container_hori2_name, new Vector3(0, 0, 0), Quaternion.identity);
        container_vert1.transform.localScale = new Vector3((float)_sampler.container["scl_x_vert"], (float)_sampler.container["scl_y_vert"], (float)_sampler.container["scl_z"]);
        container_vert1.transform.position = new Vector3((float)_sampler.container["pos_x_vert1"], (float)_sampler.container["pos_y_vert1"], (float)_sampler.container["pos_z"]);
        container_vert2.transform.localScale = new Vector3((float)_sampler.container["scl_x_vert"], (float)_sampler.container["scl_y_vert"], (float)_sampler.container["scl_z"]);
        container_vert2.transform.position = new Vector3((float)_sampler.container["pos_x_vert2"], (float)_sampler.container["pos_y_vert2"], (float)_sampler.container["pos_z"]);
        container_hori1.transform.localScale = new Vector3((float)_sampler.container["scl_x_hori"], (float)_sampler.container["scl_y_hori"], (float)_sampler.container["scl_z"]);
        container_hori1.transform.position = new Vector3((float)_sampler.container["pos_x_hori1"], (float)_sampler.container["pos_y_hori1"], (float)_sampler.container["pos_z"]);
        container_hori2.transform.localScale = new Vector3((float)_sampler.container["scl_x_hori"], (float)_sampler.container["scl_y_hori"], (float)_sampler.container["scl_z"]);
        container_hori2.transform.position = new Vector3((float)_sampler.container["pos_x_hori2"], (float)_sampler.container["pos_y_hori2"], (float)_sampler.container["pos_z"]);
        floor_y = (float)_sampler.container["pos_y_hori1"];
        ceiling_y = (float)_sampler.container["pos_y_hori2"] + 10;
        left_x = (float)_sampler.container["pos_x_vert1"];
        right_x = (float)_sampler.container["pos_x_vert2"];
        // ceiling is without collision and size is narrow
        container_hori2.GetComponent<ObiCollider>().enabled = false;

        AddStateWriter(container_vert1.name, container_vert1, is_static:true);
        AddStateWriter(container_vert2.name, container_vert2, is_static:true);
        AddStateWriter(container_hori1.name, container_hori1, is_static:true);
        AddStateWriter(container_hori2.name, container_hori2, is_static:true);
        container_vert1.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
        container_vert2.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
        container_hori1.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
        container_hori2.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
        var pos_x_bg = ((float)_sampler.container["pos_x_vert2"] + (float)_sampler.container["pos_x_vert1"]) / 2;
        var pos_y_bg = (float)_sampler.container["pos_y_vert2"];
        var scl_x_bg = (float)Math.Abs((float)(_sampler.container["pos_x_vert2"]) - (float)(_sampler.container["pos_x_vert1"]));
        var scl_y_bg = (float)Math.Abs((float)(_sampler.container["pos_y_hori2"]) - (float)(_sampler.container["pos_y_hori1"]));
        background = AddObjectFromPrefab(cfg.bg_prefab_path, "Background", new Vector3(pos_x_bg, pos_y_bg, cfg.bg_pos_z), Quaternion.identity);
        background.transform.localScale = new Vector3(scl_x_bg, scl_y_bg, 1);
        lookat = new Vector3(pos_x_bg, pos_y_bg, cfg.bg_pos_z);
        float pos_z = (float)_sampler.container["pos_z"];
        float O_x = (float)_sampler.container["O_x"];
        float O_y = (float)_sampler.container["O_y"];

        // Build sticks
        List<StickSampling.Stick> sticks = _sampler.sticks;
        int stick_num = sticks.Count;
        Dictionary<string, string> temp_dict = new ();
        for (int i = 0; i < stick_num; i++){
            var stick = sticks[i];
            if( toRemove >= 0 && this_cf_remove_name_list[toRemove] == stick.name ) continue;
            GameObject stick0;
            stick0 = AddObjectFromPrefab(cfg.stick_prefab_path, stick.name, stick.Position() + new Vector3(O_x, O_y, pos_z), Quaternion.Euler(stick.Rotation()));
            
            stick0.transform.localScale = stick.Scale();
            SetObject_P3DMColor(stick0, stick.color);
            // temperature dictionary and indicator component
            temp_dict.Add(stick0.name, stick.temperature);
            stick0.AddComponent<TemperatureIndicator>().Temperature = stick.temperature;
            AddStateWriter(stick0.name, stick0, is_static: true);
        }
        
        // Build Reservoirs
        List<Reservoir> receptors = _sampler.reservoirs;
        Dictionary<string, (Vector3, Vector3)> receptorCenterAndScale = new();
        Dictionary<string, (double, double, double, double)> receptorGuidedMinXMaxXMinYMaxY = new();
        int receptor_num = receptors.Count;
        for (int i = 0; i < receptor_num; i++){ 
            var receptor = receptors[i];
            GameObject receptor0 = AddObjectFromPrefab(cfg.receptor_prefab_path, receptor.name, receptor.position + new Vector3(O_x, O_y, pos_z), Quaternion.identity);
            receptor0.transform.localScale = receptor.scale;
            receptorCenterAndScale.Add(receptor.name, (receptor.position + new Vector3(O_x, O_y, pos_z), receptor.scale));
            // add orientator
            var stick1 = receptor.stick1;
            GameObject stick10 = AddObjectFromPrefab(cfg.stick_prefab_path, stick1.name, stick1.Position() + new Vector3(O_x, O_y, pos_z), Quaternion.Euler(stick1.Rotation()));
            stick10.transform.localScale = stick1.Scale();  
            var stick2 = receptor.stick2;
            GameObject stick20 = AddObjectFromPrefab(cfg.stick_prefab_path, stick2.name, stick2.Position() + new Vector3(O_x, O_y, pos_z), Quaternion.Euler(stick2.Rotation()));
            stick20.transform.localScale = stick2.Scale();
            // Guided Lines' minX, maxX, minY, maxY
            (double, double) minxy1 = stick1.minxy, minxy2 = stick2.minxy;
            (double, double) maxxy1 = stick1.maxxy, maxxy2 = stick2.maxxy;
            receptorGuidedMinXMaxXMinYMaxY.Add(receptor.name, (Math.Min(minxy1.Item1, minxy2.Item1) + O_x, Math.Max(maxxy1.Item1, maxxy2.Item1) + O_x, Math.Min(minxy1.Item2, minxy2.Item2) + O_y, Math.Max(maxxy1.Item2, maxxy2.Item2) + O_y));
            
            // add color / tempearture
            SetObject_P3DMColor(stick10, stick1.color);
            SetObject_P3DMColor(stick20, stick2.color);
            AddStateWriter(stick10.name, stick10, is_static:true);
            AddStateWriter(stick20.name, stick20, is_static:true);
            stick10.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
            stick20.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
            for (int j=0; j < receptor0.transform.childCount; j++){
                var go = receptor0.transform.GetChild(j).gameObject;
                go.name = receptor.name + " " + go.name; // e.g. Red Container Bottom Floor
                SetObject_P3DMColor(go, receptor.color);
                AddStateWriter(go.name, go, is_static:true);
                go.AddComponent<TemperatureIndicator>().Temperature = cfg.NORMAL;
            }
        }

        // Build Fluid
        List<Fluid> fluids = _sampler.fluids;
        int fluid_num = fluids.Count;
        obisolver = AddObjectFromPrefab(cfg.solver_prefab_path, "Solver", new Vector3(0, 0, 0), Quaternion.identity);
        emittersForPred = new();
        emittersForFactual = new();
	    Dictionary<string, float> fluidDensityDict = new();
        List<FluidSlidesTag> fluidSlidesTags = new();
        for (int i = 0; i < fluid_num; i++){
            var emitter = fluids[i];
            GameObject emitterObject = AddObjectFromPrefab(cfg.emitter_prefab_path, emitter.name, new Vector3(0, 0, 0), Quaternion.identity);
            
            AddRandTagToObject(emitterObject, typeof(FluidSlidesTag));
            FluidSlidesTag tag = emitterObject.GetComponent<FluidSlidesTag>(); 
            float fluidVis = (float)cfg.fluid_vis_temp_change_range[emitter.viscosity];
            tag.rho = emitter.density;
            tag.vis = fluidVis;
            tag.sft = emitter.surfaceTension;
            tag.capacity = emitter.amount;
            tag.fluid_color = emitter.color;
            tag.solver = obisolver;
            tag.pos_x = emitter.emitter_pos.x + O_x;
            tag.pos_y = emitter.emitter_pos.y + O_y;
            tag.pos_z = emitter.emitter_pos.z + pos_z;
            tag.speed = 0;
            tag.randV = cfg.fluid_emitter_random_v;
            tag.visualize = _this_iter_ft;
            fluidSlidesTags.Add(tag);
            fluidDensityDict.Add(emitterObject.name, emitter.density);
            AddStateWriter(emitterObject.name, emitterObject, is_softbody:true);
            AddStateWriter(emitterObject.name + " Emitter", emitterObject, is_static:true);
            for (int j=0; j < emitterObject.transform.childCount; j++){
                var go = emitterObject.transform.GetChild(j).gameObject;
                if (go.name == "Square") continue;
                go.name = go.name.Replace("EmitterPartToDelete", emitterObject.name + " Emitter"); // e.g. Red Fluid Emitter Right Wall
                AddStateWriter(go.name, go, is_static:true);
            }
            var rndrs = emitterObject.GetComponentsInChildren<MeshRenderer>();
            foreach(var rndr in rndrs){
                SetObject_P3DMColor(rndr.gameObject, emitter.color);
            }
            // open it
            emittersForFactual.Add(tag);
            if (emitter.hasPredEmitter){ // add pred emitter
                GameObject emitterPred = AddObjectFromPrefab(cfg.emitter_prefab_path, "Later Emitted " + emitter.name, emitter.pred_emitter_pos + new Vector3(O_x, O_y, pos_z), Quaternion.identity);
                AddRandTagToObject(emitterPred, typeof(FluidSlidesTag));
                FluidSlidesTag tagPred = emitterPred.GetComponent<FluidSlidesTag>(); 
                tagPred.rho = emitter.density;
                tagPred.vis = fluidVis;
                tagPred.sft = emitter.surfaceTension;
                tagPred.capacity = emitter.amount;
                tagPred.fluid_color = emitter.color;
                tagPred.solver = obisolver;
                tagPred.pos_x = emitter.pred_emitter_pos.x + O_x;
                tagPred.pos_y = emitter.pred_emitter_pos.y + O_y;
                tagPred.pos_z = emitter.pred_emitter_pos.z + pos_z;
                tagPred.speed = 0;
                tagPred.randV = cfg.fluid_emitter_random_v;
                tagPred.visualize = _this_iter_ft;
                fluidSlidesTags.Add(tagPred);
                emittersForPred.Add(tagPred);
                fluidDensityDict.Add(emitterPred.name, emitter.density);
                AddStateWriter(emitterPred.name, emitterPred, is_softbody:true);
                AddStateWriter(emitterPred.name + " Emitter", emitterPred, is_static:true);
                for (int j=0; j < emitterPred.transform.childCount; j++){
                    var go = emitterPred.transform.GetChild(j).gameObject;
                    if (go.name == "Square") continue;
                    go.name = go.name.Replace("EmitterPartToDelete", emitterPred.name + " Emitter"); // e.g. Red Fluid Emitter Right Wall
                    AddStateWriter(go.name, go, is_static:true);
                }
                var rndrspred = emitterPred.GetComponentsInChildren<MeshRenderer>();
                foreach(var rndr in rndrspred){
                    SetObject_P3DMColor(rndr.gameObject, emitter.color);
                }
            }
        }
        var HT0 = AddHeatTransportEffectOnSolver(obisolver, temp_dict, receptorCenterAndScale, receptorGuidedMinXMaxXMinYMaxY, fluidDensityDict);
        foreach(FluidSlidesTag tag in fluidSlidesTags){
            tag.HT = HT0;
        }
    }
    List<List<float>> temp_fluid_state;
    protected override List<List<float>> GetSoftbodyState(GameObject go){
        var temp = base.GetSoftbodyState(go);
        var emitter = go.GetComponent<ObiEmitter>();
        temp_fluid_state = new();// in base function they directly call GetSoftbodyGeometry tp use this
        if (emitter != null){
            // this input is fluid: output position and rotation
            var solver = obisolver.GetComponent<ObiSolver>();
            var ht = obisolver.GetComponent<HeatTransport>();
            var particlesMask = ht.perParticleCachePosition;
            if (particlesMask != null) {
                int num = emitter.solverIndices.Length;
                for (int i = 0; i<num; i++){ // local == absolute here
                    int id = emitter.solverIndices[i];
                    var pos = solver.positions[id];
                    var pos_ls = Vec2List(pos);
                    if (particlesMask[id]) {
                        if (pos_ls[2] < 0.0001f  && pos_ls[2] > -0.0001f) 
                        {
                            pos_ls[2] = 0f;
                        }
                        temp.Add(pos_ls);
                    }
                    temp_fluid_state.Add(pos_ls);
                }
            }
        }
        return temp;
    }
    protected override (List<List<float>>, List<int>) GetSoftbodyGeometry(GameObject go){
        (List<List<float>>, List<int>) temp = new();
        var emitter = go.GetComponent<ObiEmitter>();
        if (emitter != null){
            temp = GetFluidMesh(temp_fluid_state);
        }
        return temp;
    }

    protected (float, float, float, float) getRange(List<List<float>> Particles) {
        // (x1, x2, y1, y2)
        float x_1 = 10000f;
        float x_2 = -10000f;
        float y_1 = 10000f;
        float y_2 = -10000f;
        foreach(var p in Particles){    
            if (p[0] < x_1) x_1 = p[0];
            if (p[0] > x_2) x_2 = p[0];
            if (p[1] < y_1) y_1 = p[1];
            if (p[1] > y_2) y_2 = p[1];
        }
        x_1 = Mathf.Max(x_1, left_x);
        x_2 = Mathf.Min(x_2, right_x);
        y_1 = Mathf.Max(y_1, floor_y);
        y_2 = Mathf.Min(y_2, ceiling_y);
        return (x_1, x_2, y_1, y_2);
    }

    

    protected (List<List<float>>, List<int>) GetFluidMesh(List<List<float>> Particles){
        (float, float, float, float) gridRange = getRange(Particles);
        float x_1 = gridRange.Item1;
        float x_2 = gridRange.Item2;
        float y_1 = gridRange.Item3;
        float y_2 = gridRange.Item4;
        float c_x = (x_2 + x_1) / 2;
        float c_y = (y_2 + y_1) / 2;
        int gridNumX = (int)((x_2 - x_1) / cfg.gridSize) + 1;
        int gridNumY = (int)((y_2 - y_1) / cfg.gridSize) + 1;
        if (gridNumX < 2 || gridNumY < 2) return (new List<List<float>>(), new List<int>());
        float[,] map = new float[gridNumX, gridNumY];
        int extra_n = (int) ( cfg.particleSize / cfg.gridSize ) + 1;
        List<List<int>> extra = new (){new List<int>(){0, 0}};
        for (int i = 1; i < extra_n; i++) {
            for (int j = 1; j < extra_n; j++) {
                extra.Add(new List<int>(){-i, i});
                extra.Add(new List<int>(){i, -i});
                extra.Add(new List<int>(){i, i});
                extra.Add(new List<int>(){-i, -i});
            }
        }
        foreach (var p in Particles){
            int idx_x = (int)((p[0] - x_1) / cfg.gridSize);
            int idx_y = (int)((p[1] - y_1) / cfg.gridSize);
            // assign the sphere resion
            foreach (var ij in extra) { 
                int idx_x2 = idx_x + ij[0];
                int idx_y2 = idx_y + ij[1];
                if (idx_x2 < 0 || idx_x2 >= gridNumX || idx_y2 < 0 || idx_y2 >= gridNumY) continue;
                map[idx_x2, idx_y2] = 1f;
            }
        }
        
        //Generate the mesh with marching squares algorithm
        var grid = MarchingSquares.GenerateMesh(map, cfg.gridSize, shouldSmooth: false);

        // transform Vec2 to List<float>
        List<List<float>> temp = new();
        int num = grid.vertices.Count;
        var vert = grid.vertices;
        for( int i = 0; i < num; i++){
            var v = vert[i];
            temp.Add(new List<float>(){v.x + c_x, v.y + c_y, 0.0f});
        }
        
        var mesh = Standard2MeshFunc((temp, grid.triangles));
        var mesh_sim = Simplify(mesh, cfg.simplifyquality);
        return Mesh2StandardFunc(mesh_sim);
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
        AddObjectFromPrefab(cfg.bg2_prefab_path, cfg.bg2_prefab_path, new Vector3(0,-0.824999988f,0.25f), Quaternion.identity);
        _ObjManager.Remove(cfg.bg2_prefab_path);
    }

    protected override void OnIterationStart()
    {
        base.OnIterationStart();
        if (cfg.ANNO_MODE == cfg.FACTUAL){
            _scenario.framesPerIteration = cfg.framesPerIteration;
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL){
            _scenario.framesPerIteration = cfg.captureFrames;
        }
        _pre_name = cfg.pre_name;
        // Build Scene
        BuildIterScene();
    }

    protected override void OnUpdate() {
        base.OnUpdate();
        Debug.Log("Frame: " + _this_frame);
        if (cfg.ANNO_MODE == cfg.FACTUAL){ //factual mode
            if (_this_frame == 2) {
                StartEmitters(emittersForFactual);
                setCamera();
                MakeCylinders.enable = true;
            }
            if (_this_frame == cfg.emitterPredStartTime) StartEmitters(emittersForPred);
            if (_this_frame == cfg.stopCaptureTime) {
                _maincamera.SetActive(false); // stop capturing
                if (!cfg.debug) MakeCylinders.enable = false;
            }
        }
        // left assets
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL) { //counterfactual mode
            if (_this_frame == 2) {
                StartEmitters(emittersForFactual);
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
            if (co.name.Contains("EmitterPartToDelete")){
                name = name.Replace("EmitterPartToDelete", co.transform.parent.name + " Emitter");
                rot.x = 0;
                var z = scale.y;
                scale.y = scale.z;
                scale.z = z;
            }
            temp.Add(name, new Dictionary<string, List<float>>{
                {"position", Vec2List(pos)},
                {"rotation", Vec2List(rot)},
                {"scale", Vec2List(scale)}
            });
        }
        return temp;
    }

    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        if (!validity) {  return temp; }
        // mode
        temp.Add("mode", "");
        temp.Add("trackingData", vft.Summary());
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
            temp.Add("removedStickName", this_cf_remove_name_list[_this_cf_iter]);
        }
        return temp;
    }

    protected override void OnCounterFactualDone(){
        cfg.ANNO_MODE = cfg.FACTUAL;
    }
    
    protected override void OnValidFactualDone(){
        this_cf_remove_name_list = new ();
        this_cf_remove_name_list.AddRange(vft.toRemoveSticks);
        this_cf_remove = this_cf_remove_name_list.Count;
        _cf_iter_total = this_cf_remove;
        Debug.Log("OnValidFactualDone: _cf_iter_total = " + _cf_iter_total.ToString());
        foreach(string name in this_cf_remove_name_list){
            Debug.Log("OnValidFactualDone: " + name);
        }
    }
    
    protected override void OnCounterFactualIterStart(){
        if (_this_cf_iter == 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL;
        }
    }

    protected void setCamera(){
        CameraRandomizer cr = _Randomizers["CameraRandomizer"] as CameraRandomizer;

        cr.lookat = lookat;
        cr.SampleCamera();
        List<GameObject> All = new ();
        Debug.Log("setCamera" + background.name + " " + background);
        TemperatureIndicator[] gos = GameObject.FindObjectsOfType<TemperatureIndicator>();
        foreach(TemperatureIndicator go in gos){
            All.Add(go.gameObject);
        }
        All.Add(background);
        cr.EncapsulateObjects(
            All,
            _maincamera.GetComponent<Camera>(),
            padding:cfg.field_paddings
        );
    }

}
