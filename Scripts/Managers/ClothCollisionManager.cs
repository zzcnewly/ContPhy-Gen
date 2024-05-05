// is added to perception camera version 3.1 dirived from baseManager
// //ffmpeg -f image2 -i "step%d.camera.png" output.mp4
// ffmpeg -framerate 60 -i step%d.camera.png -c:v libx264 -profile:v high -crf 20 -pix_fmt yuv420p output.mp4
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
using UnityEngine.Perception.GroundTruth;
using UnityEditor.VersionControl;
using UnityEngine.UIElements;
using UnityEngine.Perception.GroundTruth.Labelers;
using UnityEngine.Rendering;
using ClothCollisionConfig;
using PlacementConfig;
using UnityEngine.VFX;
using UnityEngine.AI;
using Unity.Mathematics;
using UnityEditor.PackageManager.UI;
using System.Data;
using UnityEngine.Assertions;
using JetBrains.Annotations;
using UnityEngine.U2D;
using System.Security.Cryptography;
using Unity.VisualScripting.FullSerializer;
using Mono.Cecil;
using System.Xml.Serialization;
using UnityEngine.Rendering.HighDefinition;
using PRUtils;

[RequireComponent(typeof(Camera))]
public class ClothCollisionRandomizerTag : RandomizerTag {

}


[Serializable]
[AddRandomizerMenu("Cloth Collision Manager")]
public class ClothCollisionManager : BaseManager
{
    bool done;
    bool isFallen;
    int clothFallTime;
    PerceptionCamera pc_an;
    // private GameObject cloth;
    private GameObject floor, table;

    int sphere_color_id = 0, iso_cube_color_id = 0, iso_cubiod_color_id = 0;
    List<int> plate_color_candidates = new List<int>();
    List<int> iso_cube_color_candidates = new List<int>();
    List<int> iso_cubiod_color_candidates = new List<int>();
    
    List<Tower> towerList, towerList2;
    List<Layer> isolatedList, isolatedList2;
    ObiClothBlueprint clothBlueprintCache, clothBlueprintCache2;

    (float, float) cloth_bend, cloth_stretch, cloth_bend2, cloth_stretch2;
    float cloth_scale, cloth_scale2;
    float cloth_height, cloth_height2;
    string cloth_friction_material, cloth_friction_material2;
    GameObject clothSolver, clothSolver2;

    List<TouchmentRecorder> touchmentrecorder, touchmentrecorder2;
    ForceInCloth forceRecorder, forceRecorder2;


    int this_exchange, this_exchange2,  this_removeTower, this_removeTower2;
    List<(Vector3, Vector3)> ExchangeDist = new(), ExchangeDist2 = new();
    List<(int, int)> exchangeCFList = new (), exchangeCFList2 = new ();
    List<int> removeCFList = new (), removeCFList2 = new ();



    private UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<float> stretchingScale = new UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<float>() { };
    private UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<(float, float)> stretchPro = new UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<(float, float)>() { };
    private UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<(float, float)> bendPro = new UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<(float, float)>() { };
    private UnityEngine.Perception.Randomization.Parameters.FloatParameter rnd = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(0.0f, 1.0f) };
    private UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<string> frictionSelect= new UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<string>() { };

    public UnityEngine.Perception.Randomization.Parameters.IntegerParameter _tower_num; /// 2-5
    public UnityEngine.Perception.Randomization.Parameters.IntegerParameter _isolated_num; /// 2-5
    public UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<int> _isolated_shape = new(); /// 2-5
    public UnityEngine.Perception.Randomization.Parameters.FloatParameter   _cloth_height; /// realtive height 1.5~3.5
    public UnityEngine.Perception.Randomization.Parameters.FloatParameter   _iso_cubiod_height; /// realtive height 1.5~3.5
    public UnityEngine.Perception.Randomization.Parameters.FloatParameter   _scale_x_cuboid, _scale_z_cuboid, _scale_xyz_cube; /// The scale of the placed objects
    public UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<float>  _mass_rnd_lowF = new(), _mass_rnd_highF = new(); /// The scale of the placed objects
    public UnityEngine.Perception.Randomization.Parameters.CategoricalParameter<float>  _mass_rnd_lowF_tower = new(), _mass_rnd_highF_tower = new(); /// The scale of the placed objects
    public UnityEngine.Perception.Randomization.Parameters.Vector3Parameter _rotation_obj, _rotation_plate; /// The rotation of the placed objects
    public UnityEngine.Perception.Randomization.Parameters.Vector2Parameter _pos_sample, _pos_sample2, _pos_sample_tower;

    public class Layer {
        static public int id_allocator = 0;
        // public int layerId;
        public string name;
        public int shape;
        public float mass=1f;
        public GameObject gameObject;
        public Vector3 init_pos;
        public Vector3 init_rot;        
        public Vector3 scale;        
        public int color;
        public Vector3 start_pos;
        public Vector3 start_rot;
        public Vector3 end_pos;
        public Vector3 end_rot;
        public string poseDescription;
        public Dictionary<string, object> StateToAnnotation() {
            if (name == null || gameObject == null) {
                Debug.Log("Layer name or gameObject is null");
            }
            var output = new Dictionary<string, object>() {
                // {"layerId", layerId},
                {"name", name},
                {"colorName", SoftColor.color_name_dict[color]},
                {"colorRGB", SoftColor.color_rgb_dict[color].ToString()},
                {"shape", PlacementType.shape_name_dict[shape]},
                {"scale", Vec2List(scale)},
                {"sampledPos_Local", Vec2List(init_pos)},
                {"sampledRot", Vec2List(init_rot)},
                {"startPos", Vec2List(start_pos)},
                {"startRot", Vec2List(start_rot)},
                {"endPos", Vec2List(end_pos)},
                {"endRot", Vec2List(end_rot)},
                {"endPoseDescription", poseDescription},
            };
            if (gameObject != null) {
                output.Add("mass", gameObject.GetComponent<Rigidbody>().mass);
                output.AddRange(gameObject.GetComponent<TouchmentRecorder>().GetAll());
            }
            return output;
        }
        
        public Bounds GetXYZRangeOffline(){
            float xmin = 0;
            float xmax = 0;
            float zmin = 0;
            float zmax = 0;
            if (shape != PlacementType.Sphere) { // serve as cube / cuboid
                Quaternion rot = Quaternion.Euler(init_rot);
                Vector3 uxLocal_1 = new Vector3(-0.5f * scale.x, 0, -0.5f * scale.z);
                Vector3 uxLocal_2 = new Vector3(0.5f *  scale.x, 0, -0.5f * scale.z);
                Vector3 uxLocal_3 = new Vector3(-0.5f * scale.x, 0, 0.5f *  scale.z);
                Vector3 uxLocal_4 = new Vector3(0.5f *  scale.x, 0, 0.5f *  scale.z);
                Vector3 uxWorld_1 = rot * uxLocal_1;
                Vector3 uxWorld_2 = rot * uxLocal_2;
                Vector3 uxWorld_3 = rot * uxLocal_3;
                Vector3 uxWorld_4 = rot * uxLocal_4;
                float[] x = new float[4]{
                    uxWorld_1.x, uxWorld_2.x, uxWorld_3.x, uxWorld_4.x
                };
                float[] z = new float[4]{
                    uxWorld_1.z, uxWorld_2.z, uxWorld_3.z, uxWorld_4.z
                };
                xmin = Mathf.Min(x) + init_pos.x;
                xmax = Mathf.Max(x) + init_pos.x;
                zmin = Mathf.Min(z) + init_pos.z;
                zmax = Mathf.Max(z) + init_pos.z;
            }
            else {
                xmin = init_pos.x - scale.x / 2;
                xmax = init_pos.x + scale.x / 2;
                zmin = init_pos.z - scale.z / 2;
                zmax = init_pos.z + scale.z / 2;
            }
            Bounds bb = new Bounds(new Vector3((xmin + xmax) / 2, init_pos.y, (zmin + zmax) / 2), new Vector3(xmax - xmin, scale.y, zmax - zmin));
            return bb;
        }
    };

    public class Tower {
        static public int id_allocator = 0;
        public string name;
        public Vector2 baseXZ;
        public List<Layer> layerList;
        public Bounds GetXYZRangeOffline(){
            // get the max_x, min_x, max_y, min_y, max_z, min_z of the Tower
            Bounds bounds = new Bounds(new Vector3(0, 0, 0), Vector3.zero);
            foreach (Layer layer in layerList) {
                bounds.Encapsulate(layer.GetXYZRangeOffline());
            }
            bounds.center += new Vector3(baseXZ.x, 0, baseXZ.y);
            return bounds;
        }

        public Dictionary<string, object> StateToAnnotation() {
            Dictionary<string, object> towerDict = new Dictionary<string, object>();
            towerDict.Add("name", name);
            towerDict.Add("baseXZ", Vec2List(baseXZ));
            towerDict.Add("layerNum", layerList.Count);
            towerDict.Add("layersDownToUpList", layerList.Select(layer => layer.StateToAnnotation()).ToList());
            return towerDict;
        }


        public void GetXYZRangeOnline(Vector3 outCenter, Vector3 outSize){
            // Define an initial Bounds object with an invalid size
            Bounds bounds = new Bounds(layerList[0].gameObject.transform.position, Vector3.zero);
            // Loop through each object in the list and expand the bounds
            foreach (Layer layer in layerList) {
                GameObject obj = layer.gameObject;
                // Get the bounds of the object in world space
                Bounds objBounds = obj.GetComponent<Renderer>().bounds;
                // Expand the overall bounds to include the bounds of the current object
                bounds.Encapsulate(objBounds);
            }
            // The resulting bounds object will now contain the union of all the object bounds
            outSize = bounds.size;
            outCenter = bounds.center;
        }
        
    };

     public override void BuildScheme()
    {
        // voxel output settings
        voxel_absolute_size = cfg.voxel_absolute_size;
        realtime_voxelize  = true;

        // cF Mode
        cFMode = true;
        cfg.ANNO_MODE = cfg.FACTUAL;
        _total_valid_iter = cfg.ValidIterNum;
        _framerate = cfg.frameRate;
        UnityEngine.Random.InitState(cfg.randomSeed);
        
        //video saving logic settings here
        start_image_id = 2;
        end_image_id = cfg.framesPerIteration / (1 + cfg.framesBetweenCaptures);

        // init categories
        stretchingScale.SetOptions(cfg.stretchingScale);
        stretchPro.SetOptions(cfg.stretchPro);
        bendPro.SetOptions(cfg.bendPro);
        frictionSelect.SetOptions(cfg.friction_mat_range);
        _mass_rnd_lowF.SetOptions(cfg.mass_candidates_lowF);
        _mass_rnd_highF.SetOptions(cfg.mass_candidates_highF);
        _mass_rnd_lowF_tower.SetOptions(cfg.mass_candidates_lowF_tower);
        _mass_rnd_highF_tower.SetOptions(cfg.mass_candidates_highF_tower);
        _tower_num = new IntegerParameter() { value = new UniformSampler(cfg.tower_num_min, cfg.tower_num_max) };
        _isolated_num = new IntegerParameter() { value = new UniformSampler(cfg.iso_num_min, cfg.iso_num_max) };
        _isolated_shape.SetOptions(cfg.iso_shape_candidate);
        _iso_cubiod_height = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(cfg.cubiod_height_min, cfg.cubiod_height_max) };
        _cloth_height = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(cfg.cloth_height_addition_min, cfg.cloth_height_addition_max) };
        _scale_z_cuboid = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(cfg.cuboid_scale_z_min, cfg.cuboid_scale_z_max) };
        _scale_x_cuboid = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(cfg.cuboid_scale_x_min, cfg.cuboid_scale_x_max) };
        _scale_xyz_cube = new UnityEngine.Perception.Randomization.Parameters.FloatParameter() { value = new UniformSampler(cfg.cube_scale_min, cfg.cube_scale_max) };
        _rotation_obj = new UnityEngine.Perception.Randomization.Parameters.Vector3Parameter() { x = new UniformSampler(0,0), y = new UniformSampler(cfg.obj_rotation_y_min, cfg.obj_rotation_y_max), z = new UniformSampler(0,0) };
        _rotation_plate = new UnityEngine.Perception.Randomization.Parameters.Vector3Parameter() { x = new UniformSampler(0,0), y = new UniformSampler(cfg.plate_rotation_y_min, cfg.plate_rotation_y_max), z = new UniformSampler(0,0) };
        _pos_sample = new UnityEngine.Perception.Randomization.Parameters.Vector2Parameter() { x = new UniformSampler(cfg.pos_x_min, cfg.pos_x_max),
                                                                                             y = new UniformSampler(cfg.pos_z_min, cfg.pos_z_max) };
        _pos_sample2 = new UnityEngine.Perception.Randomization.Parameters.Vector2Parameter() { x = new UniformSampler(cfg.pos_x_min2, cfg.pos_x_max2),
                                                                                                y = new UniformSampler(cfg.pos_z_min2, cfg.pos_z_max2) };
        _pos_sample_tower = new UnityEngine.Perception.Randomization.Parameters.Vector2Parameter() { x = new UniformSampler(cfg.pos_x_min_tower, cfg.pos_x_max_tower),
                                                                                             y = new UniformSampler(cfg.pos_z_min_tower, cfg.pos_z_max_tower) };
        // init others
        string idconfig_path = "PerceptionConfigs/ClothCollisionIdLabelConfig";
        string ssconfig_path = "PerceptionConfigs/ClothCollisionSemanticSegmentationLabelConfig";
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
            ClothCollisionManager r1 = this;
            
            CameraRandomizer r5 = new CameraRandomizer();
            
            // Change the Sample Parameters
            r5.lookat = new UnityEngine.Vector3(cfg.look_at_x, cfg.look_at_y, cfg.look_at_z);
            r5.cam_angle  = new() { value = new NormalSampler(cfg.cam_lr_min, cfg.cam_lr_max, cfg.cam_lr_mean, cfg.cam_lr_std) };
            r5.cam_angle2  = new() { value = new NormalSampler(cfg.cam_ud_min, cfg.cam_ud_max, cfg.cam_ud_mean, cfg.cam_ud_std) };
            r5.cam_radius  = new() { value = new NormalSampler(cfg.cam_dist_min, cfg.cam_dist_max, cfg.cam_dist_mean, cfg.cam_dist_std) };
            r5.cam_fov  = new() { value = new UniformSampler(cfg.cam_fov_min, cfg.cam_fov_max) };

            // Add Randomizer
            AddRandomizerAtLast("MainRandomizer", r1);
            AddRandomizerAtLast("CameraRandomizer", r5);

            // Add Tag to Camera (others added when iter start)
            AddRandTagToObject(_maincamera, typeof(ClothCollisionRandomizerTag));
            AddRandTagToObject(_maincamera, typeof(CameraRandomizerTag));

            
            // Add Labeler and Set Config File
            // use two cameras to capture images and annotations respectively
            pc_an = _maincamera.GetComponent<PerceptionCamera>();
            // // set camera settings
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

    protected int Allocate_Object_A_Unique_Color(int shape){
        // The call time should not exceed the color number limit
        int result;
        if(shape == PlacementType.Cube){
            result = iso_cube_color_candidates[iso_cube_color_id];
            iso_cube_color_id ++;
            if (iso_cube_color_id >= iso_cube_color_candidates.Count){
                throw new Exception("iso_cube_color_id out of range");
            }
        }
        else if (shape == PlacementType.Cubiod){
            result = iso_cubiod_color_candidates[iso_cubiod_color_id];
            iso_cubiod_color_id ++;
            if (iso_cubiod_color_id >= iso_cubiod_color_candidates.Count){
                throw new Exception("iso_cuboid_color_id out of range");
            }
        }
        else if (shape == PlacementType.Plate){
            result = plate_color_candidates[sphere_color_id];
            sphere_color_id ++;
            if (sphere_color_id >= plate_color_candidates.Count){
                throw new Exception("iso_sphere_color_id out of range");
            }
        }
        else{
            throw new Exception("shape not supported");
        }
        return result;
    }

    protected string GetName(int color, int shape){
        // The call time should not exceed the color number limit
        if(shape == PlacementType.Cube){
            return SoftColor.color_name_dict[color] + " Cube";
        }
        else if (shape == PlacementType.Cubiod){
            return SoftColor.color_name_dict[color] + " Pillar";
        }
        else if (shape == PlacementType.Sphere){
            return SoftColor.color_name_dict[color] + " Sphere";
        }
        else if (shape == PlacementType.Plate){
            return SoftColor.color_name_dict[color] + " Plate";
        }
        else{
            throw new Exception("shape not supported");
        }
    }

     public object SampleAtOrigin(int layer_num, bool isTower, bool mass_tiny) {
        if (layer_num < 2 && !isTower) {
            // isolated object
            int shape = _isolated_shape.Sample();
            float scale_z = _scale_z_cuboid.Sample();
            float scale_x = _scale_x_cuboid.Sample();
            float scale_y = _iso_cubiod_height.Sample() ;
            Layer obj = new Layer() {
                shape = shape,
                init_pos = new Vector3(0, scale_y/2, 0),
                init_rot = _rotation_plate.Sample(),
                scale = new Vector3(scale_x, scale_y, scale_z),
                mass = mass_tiny? _mass_rnd_lowF.Sample() : _mass_rnd_highF.Sample(),
            };
            obj.color = Allocate_Object_A_Unique_Color(shape);
            obj.name = GetName(obj.color, shape);
            return obj;
        }
        else{ // sample tower
            bool is_top_sphere = false;
            if (rnd.Sample() < cfg.top_sphere_prob) {
                is_top_sphere = true;
            }
            List<Layer> temp_layers = new ();
            int cubiod_num = is_top_sphere ? layer_num - 1 : layer_num;
            float height = 0;
            float sphere_size = is_top_sphere ? _scale_xyz_cube.Sample() : 0;
            while(true){
                Layer layer = new Layer();
                float scale_xz = _scale_xyz_cube.Sample();
                float scale_y = scale_xz;
                if (height + scale_y > cfg.max_tower_height - sphere_size) {
                    break;
                }
                height += scale_y;

                layer.shape = PlacementType.Cube;
                layer.init_rot = _rotation_obj.Sample();
                layer.scale = new Vector3(scale_xz, scale_y, scale_xz);
                layer.mass = mass_tiny? _mass_rnd_lowF_tower.Sample() : _mass_rnd_highF_tower.Sample();
                temp_layers.Add(layer);
            }
            if (temp_layers.Count < cubiod_num) {
                throw new Exception("Max_tower_height is too small or layer_num/layer_scale is too large");
            }
            // delete or merge some layers
            int delete_num = temp_layers.Count - cubiod_num;
            for (int i = 0; i < delete_num; i++) {
                if (rnd.Sample() < cfg.delete_rate_compare_to_merge) {
                    // delete
                    int delete_id = (int)(rnd.Sample() * temp_layers.Count);
                    temp_layers.RemoveAt(delete_id);
                }
                else {
                    // merge
                    int merge_id = (int)(rnd.Sample() * (temp_layers.Count-1));
                    Vector3 temp = temp_layers[merge_id].scale;
                    temp_layers[merge_id].scale = new Vector3(temp.x, temp.y + temp_layers[merge_id + 1].scale.y, temp.z);
                    temp_layers[merge_id].shape = PlacementType.Cubiod;
                    temp_layers.RemoveAt(merge_id + 1);
                }
            }
            // add top sphere
            if (is_top_sphere) {
                Layer layer = new Layer();
                float scale = sphere_size;
                layer.shape = PlacementType.Sphere;
                layer.init_rot = _rotation_obj.Sample();
                layer.scale = new Vector3(scale, scale, scale);
                layer.mass =  mass_tiny? _mass_rnd_lowF_tower.Sample() : _mass_rnd_highF_tower.Sample();
                temp_layers.Add(layer);
            }
            // assign xyz positions
            temp_layers[0].init_pos = new Vector3(0, temp_layers[0].scale.y / 2, 0);
            float this_height = temp_layers[0].scale.y;
            for(int i=1; i<temp_layers.Count; i++){
                float this_h = temp_layers[i].scale.y;
                // sample XZ point in support area
                float scale = temp_layers[i - 1].scale.x;
                float par_x = temp_layers[i - 1].init_pos.x;
                float par_z = temp_layers[i - 1].init_pos.z;
                float radius = scale / 2 * cfg.support_area_squeeze_rate;
                while(true){
                    float r = radius * rnd.Sample();
                    float theta = 2 * Mathf.PI * rnd.Sample();
                    float pos_x = par_x + r * Mathf.Cos(theta);
                    float pos_z = par_z + r * Mathf.Sin(theta);
                    bool ok = true;
                    for (int j = 0; j < i - 1; j++) {
                        float dist = Mathf.Sqrt(Mathf.Pow(pos_x - temp_layers[j].init_pos.x, 2) + Mathf.Pow(pos_z - temp_layers[j].init_pos.z, 2));
                        if (dist > temp_layers[j].scale.x / 2 * cfg.support_area_squeeze_rate) {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) {
                        temp_layers[i].init_pos = new Vector3(pos_x, this_height + this_h / 2, pos_z);
                        break;
                    }

                }
                this_height += this_h;
            }
            // assign colors
            for (int i=0; i < temp_layers.Count; i++) {
                temp_layers[i].color = Allocate_Object_A_Unique_Color(temp_layers[i].shape);
            }
            // give (unique) name
            for (int i=0; i < temp_layers.Count; i++) {
                temp_layers[i].name = GetName(temp_layers[i].color, temp_layers[i].shape);
            }

            // return
            Tower outTower = new Tower();
            string name = layer_num.ToString() + "-Object Tower";
            outTower.name = name;
            outTower.layerList = temp_layers;

            return outTower;
        }
    }


    public void RefreshColorPool(out List<int> candidates, out int id){
        id = 0;
        candidates = new();
        candidates.AddRange(cfg.cube_color_range);
        // shuffle the color list
        for (int i = 0; i < candidates.Count; i++) {
            int temp = candidates[i];
            int randomIndex = UnityEngine.Random.Range(i, candidates.Count);
            candidates[i] = candidates[randomIndex];
            candidates[randomIndex] = temp;
        }
        candidates.AddRange(cfg.cube_color_range_plus);
    }

    protected void SampleOneOfTwo(out List<Tower> towerList, out List<Layer> isolatedList, out float cloth_height, string cloth_friction_material,
                                    out List<(Vector3, Vector3)> ExchangeDist, out List<(int, int)> exchangeCFList, out List<int> removeCFList){
        
        towerList = new();
        isolatedList = new();
        List<int> layer_num = new();
        layer_num.AddRange(cfg.tower_layer_num);

        bool tiny_mass_mode = true;
        if (cloth_friction_material == cfg.fric_high) {
            tiny_mass_mode = false;
        }
        // shuffle the list
        for (int i = 0; i < layer_num.Count; i++) {
            int temp = layer_num[i];
            int randomIndex = UnityEngine.Random.Range(i, layer_num.Count);
            layer_num[i] = layer_num[randomIndex];
            layer_num[randomIndex] = temp;
        }
        // tower sampling
        int tower_num = _tower_num.Sample();
        for (int i = 0; i < tower_num; i++){
            towerList.Add(SampleAtOrigin(layer_num[i], true, tiny_mass_mode) as Tower);
        }
        // isolated sampling
        int iso_num = _isolated_num.Sample();
        for (int i = 0; i < iso_num; i++){
            isolatedList.Add(SampleAtOrigin(1, false, tiny_mass_mode) as Layer);
        }
        tower_num = towerList.Count;
        iso_num = isolatedList.Count;
        // assign positions
        List<Bounds> occupied_bounds = new();
        List<int> todelete = new();
        for (int i = 0; i < tower_num; i++){
            var tower = towerList[i];
            var tower_bounds = tower.GetXYZRangeOffline();
            bool is_collide = true;
            int try_num = 0;
            var basexz = _pos_sample_tower.Sample();
            while (is_collide){
                is_collide = false;
                basexz = _pos_sample_tower.Sample();
                tower_bounds.center = new Vector3(basexz.x, tower_bounds.center.y, basexz.y);
                for (int j = 0; j < occupied_bounds.Count; j++){
                    if (tower_bounds.Intersects(occupied_bounds[j])){
                        is_collide = true;
                        try_num ++;
                        break;
                    }
                }
                if (try_num > cfg.upper_limit_try_time){
                    todelete.Add(i);
                    break;
                }
            }
            if (try_num > cfg.upper_limit_try_time){ // after we will delete this tower 
                continue;
            }
            tower.baseXZ = basexz;
            occupied_bounds.Add(tower_bounds);
        }

        // change the rotation of left one
        isolatedList[0].init_rot = -isolatedList[0].init_rot;

        // delete
        int delete_num = todelete.Count;
        for (int i = 0; i < todelete.Count; i++){
            towerList.RemoveAt(todelete[delete_num - 1 - i]);
        }
        // assign isolated pos
        todelete = new();
        var ls_mass = tiny_mass_mode? cfg.mass_candidates_lowF : cfg.mass_candidates_highF;
        int idx_temp = rnd.Sample() < 0.5f? 0 : 1;
        for (int i = 0; i < iso_num; i++){
            var iso = isolatedList[i];  
            var iso_bounds = iso.GetXYZRangeOffline();
            bool is_collide = true;
            int try_num = 0;
            var basexz = i == 0 ? _pos_sample.Sample() : _pos_sample2.Sample();
            while (is_collide){
                is_collide = false;
                basexz = i == 0 ? _pos_sample.Sample() : _pos_sample2.Sample();
                iso_bounds.center = new Vector3(basexz.x, iso_bounds.center.y, basexz.y);
                for (int j = 0; j < occupied_bounds.Count; j++){
                    if (iso_bounds.Intersects(occupied_bounds[j])){
                        is_collide = true;
                        try_num ++;
                        break;
                    }
                }
                if (try_num > cfg.upper_limit_try_time){
                    todelete.Add(i);
                    break;
                }
            }
            if (try_num > cfg.upper_limit_try_time){ // after we will delete this tower 
                continue;
            }
            iso.init_pos = new Vector3(basexz.x, iso.init_pos.y, basexz.y);
            occupied_bounds.Add(iso_bounds);
        }
        // delete   
        delete_num = todelete.Count;
        for (int i = 0; i < todelete.Count; i++){
            isolatedList.RemoveAt(todelete[delete_num - 1 - i]);
        }

        // move 1-object-tower's object to isolated List
        bool checkAllIs2Obj;
        do {
            checkAllIs2Obj = true; 
            for (int i = 0; i < towerList.Count; i++){
                var tower = towerList[i];
                if (tower.layerList.Count == 1) {
                    Layer layerTemp = tower.layerList[0];
                    layerTemp.init_pos += new Vector3(tower.baseXZ.x, 0, tower.baseXZ.y);
                    isolatedList.Add(layerTemp);
                    towerList.RemoveAt(i);
                    checkAllIs2Obj = false;
                    break;
                }
            }
        } while (!checkAllIs2Obj);
        // check tower name to demix
        tower_num = towerList.Count;
        iso_num = isolatedList.Count;

        // sample init free fall height
        cloth_height = _cloth_height.Sample();


        // sample CF mode
        exchangeCFList = new();
        ExchangeDist = new();
        removeCFList = new();
        for (int i = 0; i < iso_num; i++) {
            if (isolatedList[i].shape == PlacementType.Plate){
                removeCFList.Add(i + tower_num);
            }
        }
        
    }

    protected void DisplaceAndRenameSet(List<Tower> towerList, List<Layer> isolatedList, float displace){
        string afterfix = " (Right)";
        if (displace < 0) afterfix = " (Left)";
        // displace the towerList and isolatedList
        for (int i = 0; i < towerList.Count; i++) {
            var tower = towerList[i];
            tower.baseXZ = new Vector2(tower.baseXZ.x + displace, tower.baseXZ.y);
            // add (left) or (right) at the end of the tower name and each layer name
            tower.name = tower.name + afterfix;
        }
        for (int i = 0; i < isolatedList.Count; i++) {
            var iso = isolatedList[i];
            iso.init_pos = new Vector3(iso.init_pos.x + displace, iso.init_pos.y, iso.init_pos.z);
        }
    }

    public bool haveIntersection(List<Bounds> bounds){
        // check if there is any intersection between the bounds
        // if there is, return true
        // else return false
        for (int i = 0; i < bounds.Count; i++) {
            for (int j = i + 1; j < bounds.Count; j++) {
                if (bounds[i].Intersects(bounds[j])) return true;
            }
        }
        return false;
    }

    protected bool CheckConstructionsValidAndSaveStartStates(List<Tower> towerList, List<Layer> isolatedList){ 
        // right after isAllStatic() == true
        // here we save the start pos and rot of each object and check the validity
        bool ok = true;
        for (int i = 0; i < towerList.Count; i++) {
            var tower = towerList[i];
            for (int j = 0; j < tower.layerList.Count; j++) {
                var layer = tower.layerList[j];
                if (layer.gameObject == null){
                    continue;
                }
                Vector3 start_pos = layer.gameObject.transform.position;
                Vector3 start_rot = layer.gameObject.transform.rotation.eulerAngles;
                layer.start_pos = start_pos;
                layer.start_rot = start_rot;
                if (Mathf.Abs(start_pos.y - layer.init_pos.y) > cfg.max_tolerant_starting_height_moving){
                    ok = false; return ok;
                }
            }
        }
        for (int i = 0; i < isolatedList.Count; i++) {
            var iso = isolatedList[i];
            if (iso.gameObject == null){
                continue;
            }
            Vector3 start_pos = iso.gameObject.transform.position;
            Vector3 start_rot = iso.gameObject.transform.rotation.eulerAngles;
            iso.start_pos = start_pos;
            iso.start_rot = start_rot;
            if (Mathf.Abs(start_pos.y - iso.init_pos.y) > cfg.max_tolerant_starting_height_moving){
                ok = false; return ok;
            }
        }
        return ok;
    }

    protected void RefreshCache(List<Tower> towerList, List<Layer> isolatedList){
        for (int i = 0; i < towerList.Count; i++) {
            var tower = towerList[i];
            for (int j = 0; j < tower.layerList.Count; j++) {
                var layer = tower.layerList[j];
                layer.start_pos = new Vector3();
                layer.start_rot = new Vector3();
                layer.end_pos = new Vector3();
                layer.end_rot = new Vector3();
                layer.gameObject = null;
            }
        }
        for (int i = 0; i < isolatedList.Count; i++) {
            var iso = isolatedList[i];
            iso.start_pos = new Vector3();
            iso.start_rot = new Vector3();
            iso.end_pos = new Vector3();
            iso.end_rot = new Vector3();
            iso.gameObject = null;
        }
    }
    
    protected string getPoseDescription(GameObject go){
        var vec = go.transform.TransformVector(Vector3.up);
        if (Math.Abs(vec.y)<cfg.supine_thres){
            return cfg.SUPINE;
        }
        else if (Math.Abs(vec.x) > cfg.leaning_thres || Math.Abs(vec.z) > cfg.leaning_thres){
            if(vec.y < 0) return cfg.SWOOPING;
            else return cfg.RECLINING;
        }
        else if (vec.y > 0){
            return cfg.UPRIGHT;
        }
        else{
            return cfg.OTHER;
        }
    }

    protected void SaveObjectEndStates(List<Tower> towerList, List<Layer> isolatedList){
        // right before the iter ends (total frame-1)
        // here we save the end pos and rot of each object
        for (int i = 0; i < towerList.Count; i++) {
            var tower = towerList[i];
            for (int j = 0; j < tower.layerList.Count; j++) {
                var layer = tower.layerList[j];
                if (layer.gameObject == null){
                    continue;
                }
                Vector3 end_pos = layer.gameObject.transform.position;
                Vector3 end_rot = layer.gameObject.transform.rotation.eulerAngles;
                layer.end_pos = end_pos;
                layer.end_rot = end_rot;
                layer.poseDescription = getPoseDescription(layer.gameObject);
            }
        }
        for (int i = 0; i < isolatedList.Count; i++) {
            var iso = isolatedList[i];
            if (iso.gameObject == null){
                continue;
            }
            Vector3 end_pos = iso.gameObject.transform.position;
            Vector3 end_rot = iso.gameObject.transform.rotation.eulerAngles;
            iso.end_pos = end_pos;
            iso.end_rot = end_rot;
            iso.poseDescription = getPoseDescription(iso.gameObject);
        }
        Debug.Log("End state saved");
    }

    protected void SaveClothEndStates(GameObject os, ObiClothBlueprint target_ocb){
        var cloth = os.GetComponentInChildren<ObiCloth>();
        cloth.SaveStateToBlueprint(target_ocb);
    }

    protected void ClothSet(string name, string clothname, int id, 
                            (float, float) cloth_bend, (float, float) cloth_stretch, float cloth_scale, 
                            string cloth_friction_material, float x_displace, float y_displace, 
                            List<Tower> towerList, List<Layer> isolatedList, 
                            List<TouchmentRecorder> tr, out ForceInCloth forceRecorder){
        // if the initial sampled assignment is not stable, it's invalid 
        if (!CheckConstructionsValidAndSaveStartStates(towerList, isolatedList)){
            StopThisIterImmediatelyAndInvalidIt(false);
        }
        Vector3 clothpos = new Vector3(cfg.cloth_pos.x + x_displace, cfg.cloth_pos.y + y_displace, cfg.cloth_pos.z);
        GameObject cloth_1 = AddObjectFromPrefab(cfg.cloth_path, name, clothpos, cfg.cloth_rotation); // TODO check the pos correction
        (cloth_1.GetComponent<ObiFixedUpdater>()).substeps = cfg.cloth_substeps;
        ObiSolver os = cloth_1.GetComponent<ObiSolver>();
        if (id == 0) clothSolver = cloth_1; else clothSolver2 = cloth_1;
        os.parameters.sleepThreshold = cfg.sleepThreshold;
        os.parameters.damping = cfg.damping;
        os.parameters.maxDepenetration = cfg.maxDepenetration;
        GameObject cloth_2 = cloth_1.transform.Find("Cloth").gameObject;
        cloth_2.name = clothname;
        // sample and set cloth properties
        ObiCloth oc = cloth_2.GetComponent<ObiCloth>();
        oc.stretchingScale = cloth_scale;
        oc.stretchCompliance = cloth_stretch.Item1;
        oc.maxCompression = cloth_stretch.Item2;
        oc.bendCompliance = cloth_bend.Item1;
        oc.maxBending = cloth_bend.Item2;
        AddStateWriter(clothname, cloth_1, is_softbody: true);
        // set friction
        string f = cloth_friction_material;
        ObiCollisionMaterial ocm = Resources.Load<ObiCollisionMaterial>(f);
        oc.collisionMaterial = ocm; // cloth materials
        GameObject cloth_handle = cloth_1.transform.Find("Controller").gameObject;
        var handle = cloth_handle.AddComponent<ClothMotion>();
        // add tracker
        cloth_1.AddComponent<ClothPointTracker>().solver = os;
        // [idle now] initial the blueprint cache
        if (id == 0) clothBlueprintCache = Resources.Load<ObiClothBlueprint>(cfg.cloth_blueprint_path1);
        else clothBlueprintCache2 = Resources.Load<ObiClothBlueprint>(cfg.cloth_blueprint_path2);
        // add random material if ft
        if (cfg.ANNO_MODE == cfg.FACTUAL) cloth_2.AddComponent<MaterialChanger>();
        // assign touchment recorder
        var clothRecorder = cloth_1.AddComponent<particleTouchmentRecorder>();
        clothRecorder.manager = this;
        for (int i = 0; i < tr.Count; i++){
            tr[i].touchmentRecorder = clothRecorder;
        }
        // assign force recorder
        forceRecorder = cloth_2.AddComponent<ForceInCloth>();
    }

    protected void ClothFall(bool isLeft){
        GameObject cloth;
        if (isLeft){ cloth = clothSolver.transform.GetComponentInChildren<ObiCloth>().gameObject; }
        else { cloth = clothSolver2.transform.GetComponentInChildren<ObiCloth>().gameObject; }
        cloth.GetComponent<ObiParticleAttachment>().enabled = false;
    }


    protected void AssignOthersFrictionAndStateSaving(string other_friction_material){
        // set friction
        string f = other_friction_material;
        ObiCollisionMaterial ocm = Resources.Load<ObiCollisionMaterial>(f);
        // floor and objects
        floor.GetComponent<ObiCollider>().CollisionMaterial = ocm;
        table.GetComponent<ObiCollider>().CollisionMaterial = ocm;
        var list = GameObject.FindObjectsOfType<TouchmentRecorder>();
        foreach(TouchmentRecorder tr in list){
            GameObject go = tr.gameObject;
            go.GetComponent<ObiCollider>().CollisionMaterial = ocm;
            AddStateWriter(go.name, go);
        }
        // add Table to 3d writer
        AddStateWriter(table.name, floor);
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

    protected void ClothDenoise(GameObject clothSolver){
        clothSolver.GetComponent<ObiSolver>().parameters.sleepThreshold = cfg.sleepThresholdSettled;
    }


    protected void SetObject_P3DMColor(GameObject go, int color){
        go.GetComponent<MeshRenderer>().material.SetColor("_BASE_COLOR", SoftColor.color_rgb_dict[color]);
    }


    protected void SetObjects(List<Tower> towerList, List<Layer> isolatedList, List<(int, int)>exchangeCFList, List<(Vector3, Vector3)> ExchangeDist, List<int> removeCFList, out List<TouchmentRecorder> tr, int toRemove=-1, int toExchange_id=-1){
        tr = new();
        int id0 = -1, id1 = -1;
        Vector3 dist0 = new Vector3(), dist1 = new Vector3();
        // cf mode - exchange
        if (toExchange_id > -1) {
            id0 = exchangeCFList[toExchange_id].Item1;
            id1 = exchangeCFList[toExchange_id].Item2;
            dist0 = ExchangeDist[toExchange_id].Item1;
            dist1 = ExchangeDist[toExchange_id].Item2;
        }
        // place objects and towers
        for (int i = 0; i < towerList.Count; i++){
            Vector3 cfOffset = new Vector3(0, 0, 0);
            if (i == id0) cfOffset = dist0;
            else if (i == id1) cfOffset = dist1;
            if(toRemove == i) continue;
            var tower = towerList[i];
            string tower_name = tower.name;
            int layer_num = tower.layerList.Count;
            float x_base = tower.baseXZ.x;
            float z_base = tower.baseXZ.y;
            Vector3 Transition = new Vector3(x_base, 0, z_base) + cfOffset;
            for (int j=0; j < layer_num; j++){
                var layer = tower.layerList[j];
                GameObject go = InstantiatePrimitive(layer.shape);
                layer.gameObject = go;
                go.name = layer.name;
                go.transform.position = layer.init_pos + Transition;
                go.transform.rotation = Quaternion.Euler(layer.init_rot);
                go.transform.localScale = layer.scale;
                go.AddComponent<Rigidbody>().mass = layer.mass;
                var tr_ = go.AddComponent<TouchmentRecorder>();
                tr_.manager = this;
                tr.Add(tr_);
                SetObject_P3DMColor(go, layer.color);
                AddObject(go);
                ObiCollider oco = go.AddComponent(typeof(ObiCollider)) as ObiCollider;
                oco.Thickness = cfg.placement_thickness;
            }
        }
        for (int i = 0; i < isolatedList.Count; i++){
            if(toRemove == i + towerList.Count){continue;}
            Vector3 cfOffset = new Vector3(0, 0, 0);
            if (i + towerList.Count == id1) cfOffset = dist1;
            else if (i + towerList.Count == id0) cfOffset = dist0;
            // place iso objects
            var layer = isolatedList[i];
            GameObject go = InstantiatePrimitive(layer.shape);
            layer.gameObject = go;
            go.name = layer.name;
            go.transform.position = layer.init_pos + cfOffset;
            go.transform.rotation = Quaternion.Euler(layer.init_rot);
            go.transform.localScale = layer.scale;
            var tr_ = go.AddComponent<TouchmentRecorder>();
            tr_.manager = this;
            tr.Add(tr_);
            go.AddComponent<Rigidbody>().mass = layer.mass;
            SetObject_P3DMColor(go, layer.color);
            AddObject(go);
            ObiCollider oco = go.AddComponent(typeof(ObiCollider)) as ObiCollider;
            oco.Thickness = cfg.placement_thickness;
        }
    }

    protected void SamplePhysicalProperties_Of_Two(out (float, float) cloth_bend, out (float, float) cloth_stretch, out float cloth_scale, out string cloth_friction_material,
                                                    out (float, float) cloth_bend2, out (float, float) cloth_stretch2, out float cloth_scale2, out string cloth_friction_material2){
        // to make sure the two cloth are different in all properties
        // sample cloth // sample friction material before hand
        cloth_bend = bendPro.Sample(); 
        cloth_scale = stretchingScale.Sample();
        cloth_stretch = stretchPro.Sample(); 
        cloth_friction_material = frictionSelect.Sample();
        // sample cloth2 must be different from cloth1 except the scale
        cloth_bend2 = bendPro.Sample();
        cloth_scale2 = stretchingScale.Sample();
        cloth_stretch2 = stretchPro.Sample();
        cloth_friction_material2 = frictionSelect.Sample();
        while (cloth_friction_material2.Equals(cloth_friction_material)){
            cloth_friction_material2 = frictionSelect.Sample();
        }
        while (cloth_bend2.Equals(cloth_bend)){
            cloth_bend2 = bendPro.Sample();
        }
        while (cloth_stretch2.Equals(cloth_stretch)){
            cloth_stretch2 = stretchPro.Sample();
        }
    }

    protected override void BuildIterScene(){
        isFallen = false; 
        clothFallTime = 999999;
        done = false;
        if (_this_iter_ft){
            SamplePhysicalProperties_Of_Two(out cloth_bend, out cloth_stretch, out cloth_scale, out cloth_friction_material,
                                            out cloth_bend2, out cloth_stretch2, out cloth_scale2, out cloth_friction_material2);
            RefreshColorPool(out plate_color_candidates, out sphere_color_id);
            RefreshColorPool(out iso_cube_color_candidates, out iso_cube_color_id);
            RefreshColorPool(out iso_cubiod_color_candidates, out iso_cubiod_color_id);
            SampleOneOfTwo(out towerList, out isolatedList, out cloth_height, cloth_friction_material, out ExchangeDist, out exchangeCFList, out removeCFList);
            SampleOneOfTwo(out towerList2, out isolatedList2, out cloth_height2, cloth_friction_material2, out ExchangeDist2, out exchangeCFList2, out removeCFList2);
            DisplaceAndRenameSet(towerList, isolatedList, -cfg.two_sets_x_interval);
            DisplaceAndRenameSet(towerList2, isolatedList2, cfg.two_sets_x_interval);
            // build scene
            SetObjects(towerList, isolatedList, exchangeCFList, ExchangeDist, removeCFList, out touchmentrecorder);
            SetObjects(towerList2, isolatedList2, exchangeCFList2, ExchangeDist2, removeCFList2, out touchmentrecorder2);
        }
        else{ // counterfactual mode: ex -> ex2 -> rm -> rm2
            // lessen the frame number to make it efficient 
            _scenario.framesPerIteration -= cfg.frameSetCloth - cfg.frameCFSetCloth; 
            if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange){ // firster
                SetObjects(towerList, isolatedList, exchangeCFList, ExchangeDist, removeCFList, out touchmentrecorder, toExchange_id: _this_cf_iter);
            }
            else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange2){ // firster
                SetObjects(towerList2, isolatedList2, exchangeCFList2, ExchangeDist2, removeCFList2, out touchmentrecorder2, toExchange_id: _this_cf_iter - this_exchange);
            }
            else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower){
                SetObjects(towerList, isolatedList, exchangeCFList, ExchangeDist, removeCFList, out touchmentrecorder, toRemove: removeCFList[_this_cf_iter - this_exchange - this_exchange2]);
            }
            else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower2){
                SetObjects(towerList2, isolatedList2, exchangeCFList2, ExchangeDist2, removeCFList2, out touchmentrecorder2, toRemove: removeCFList2[_this_cf_iter - this_exchange - this_exchange2 - this_removeTower]);
            }
        }
        // SetPatient();
    }

    public GameObject InstantiatePrimitive(int prefab_type) {
        // Load from Procedural Primitives
        GameObject temp;
        switch (prefab_type) {
            case PlacementType.Cube:
                temp = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(PlacementPrefabs.type_prefab_dict[PlacementType.Cube]));
                temp.name = "Box";
                temp.AddComponent(typeof(BoxCollider));            
                break;
            case PlacementType.Sphere:
                temp = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(PlacementPrefabs.type_prefab_dict[PlacementType.Sphere]));
                temp.name = "Sphere";
                temp.AddComponent(typeof(SphereCollider));            
                break;
            case PlacementType.Cubiod:
                temp = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(PlacementPrefabs.type_prefab_dict[PlacementType.ChamferCube]));
                temp.name = "Cuboid";
                temp.AddComponent(typeof(BoxCollider));            
                break;
            case PlacementType.Plate:
                temp = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(PlacementPrefabs.type_prefab_dict[PlacementType.ChamferCube]));
                temp.name = "Plate";
                temp.AddComponent(typeof(BoxCollider));            
                break;
            case PlacementType.Rod:
                temp = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(PlacementPrefabs.type_prefab_dict[PlacementType.ChamferCube]));
                temp.name = "Rod";
                temp.AddComponent(typeof(BoxCollider));            
                break;
            default:
                temp = (GameObject)UnityEngine.Object.Instantiate(Resources.Load(PlacementPrefabs.type_prefab_dict[PlacementType.Cube]));
                temp.name = "otherShape";
                temp.AddComponent(typeof(BoxCollider));            
                break;
        }
        return temp;
    }

    protected override List<List<float>> GetSoftbodyState(GameObject go){
        var _positions1 = base.GetSoftbodyState(go);
        var temp = go.GetComponent<ObiSolver>();
        if (temp != null){
            temp.positions.ToList().ForEach(x => _positions1.Add(Vec2List(temp.transform.TransformPoint(x))));
        }
        return _positions1;
    }

    protected override (List<List<float>>, List<int>) GetSoftbodyGeometry(GameObject go){
        var temp = base.GetSoftbodyGeometry(go);
        if (go.GetComponent(typeof(ObiSolver)) != null){
            var meshfil = go.GetComponentInChildren<ObiCloth>().GetComponent<MeshFilter>();
            var toret = Mesh2StandardFunc(meshfil.sharedMesh, meshfil.transform);
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
        // Build Scene
        GameObject.DestroyImmediate(GameObject.Find("TestEnvironment"));
        GameObject env = AddObjectFromPrefab("Prefabs/Cloth Collision Blocks/Cloth/Floor", "Floor", cfg.room_pos, cfg.room_rotation);
        env.transform.localScale = cfg.room_scale;
        floor = env;
        table = env.transform.Find("ModernRoom").transform.Find("Table").gameObject;
        _maincamera.transform.position = cfg.camera_pos;
        AddObjectFromPrefab(cfg.dirlight_path, "Directional Light", cfg.light_pos, cfg.light_rotation);
        _ObjManager.Remove( "Floor" );
        _ObjManager.Remove( "Directional Light" );
    }

    protected override void OnIterationStart()
    {
        base.OnIterationStart();
        _pre_name = cfg.scene_name;
        // Sampling
        done = false;
        _scenario.framesPerIteration = cfg.framesPerIteration;
        // Build Scene
        BuildIterScene();
    }

    void PrepareEndOutData(List<Tower> towerList, List<Layer> isolatedList, GameObject clothSolver, ObiClothBlueprint clothBlueprintCache, ForceInCloth forceRecorder){
        // fixed duration time
        SaveObjectEndStates(towerList, isolatedList);
        forceRecorder.on = true;
    }

    protected void setCamera(){
        Debug.Log("all");
        CameraRandomizer cr = _Randomizers["CameraRandomizer"] as CameraRandomizer;
        cr.SampleCamera();
        TouchmentRecorder[] gos = GameObject.FindObjectsOfType<TouchmentRecorder>();
        List<GameObject> All = new ();
        foreach(TouchmentRecorder go in gos){
            All.Add(go.gameObject);
        }
        Debug.Log("all:" + All.Count);
        cr.EncapsulateObjects(
            All,
            _maincamera.GetComponent<Camera>(),
            padding:cfg.field_paddings
        );
    }

    protected void cameraRotation(){
        if (_this_frame == 2){
            Debug.Log("all:camera");
            setCamera();
        }
    }

    protected override void OnUpdate() {
        base.OnUpdate();
        Debug.Log("Frame: " + _this_frame);
        if (cfg.ANNO_MODE == cfg.FACTUAL){ //factual mode
            if (_this_frame % 10 == 0) { // set off cloths
                if ((!isFallen) && (/*isAllStatic()*/_this_frame == cfg.frameSetCloth)){
                    isFallen = true; clothFallTime = _this_frame;
                    ClothSet("obi solver (Left)", "Cloth (Left)", 0, cloth_bend, cloth_stretch, cloth_scale, cloth_friction_material, -cfg.two_sets_x_interval, cloth_height, towerList, isolatedList, touchmentrecorder, out forceRecorder);
                    ClothSet("obi solver (Right)", "Cloth (Right)", 1, cloth_bend2, cloth_stretch2, cloth_scale2, cloth_friction_material2, cfg.two_sets_x_interval, cloth_height2, towerList2, isolatedList2, touchmentrecorder2, out forceRecorder2);
                    AssignOthersFrictionAndStateSaving(cfg.fric_high);
                }
            }
            if (_this_frame == cfg.frameDropCloth){
                ClothFall(true);
                ClothFall(false);
            }
            // camera
            cameraRotation();
            if ((_this_frame == cfg.frameEndCheckFT)) {
                PrepareEndOutData(towerList, isolatedList, clothSolver, clothBlueprintCache, forceRecorder); // actually fixed duration time
                PrepareEndOutData(towerList2, isolatedList2, clothSolver2, clothBlueprintCache2, forceRecorder2);
            }
        }
        // left assets
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange || cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower) { //counterfactual mode
            if (_this_frame % 10 == 0) {
                if ((!isFallen) && (/*isAllStatic()*/_this_frame == cfg.frameCFSetCloth)){
                    isFallen = true; clothFallTime = _this_frame;
                    ClothSet("obi solver (Left)", "Cloth (Left)", 0, cloth_bend, cloth_stretch, cloth_scale, cloth_friction_material, -cfg.two_sets_x_interval, cloth_height, towerList, isolatedList, touchmentrecorder, out forceRecorder);
                    AssignOthersFrictionAndStateSaving(cfg.fric_high);
                }
            }
            if (_this_frame == cfg.frameDropCloth - cfg.frameSetCloth + cfg.frameCFSetCloth){
                ClothFall(true);
            }
            if ((_this_frame == cfg.frameEndCheckCF)) {
                PrepareEndOutData(towerList, isolatedList, clothSolver, clothBlueprintCache, forceRecorder); // actually fixed duration time
            }
        }
        // right assets
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange2 || cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower2) { //counterfactual mode
            if (_this_frame % 10 == 0) {
                if ((!isFallen) && (/*isAllStatic()*/_this_frame == cfg.frameCFSetCloth)){
                    isFallen = true; clothFallTime = _this_frame;
                    ClothSet("obi solver (Right)", "Cloth (Right)", 1, cloth_bend2, cloth_stretch2, cloth_scale2, cloth_friction_material2, cfg.two_sets_x_interval, cloth_height2, towerList2, isolatedList2, touchmentrecorder2, out forceRecorder2);
                    AssignOthersFrictionAndStateSaving(cfg.fric_high);
                }
            }
            if (_this_frame == cfg.frameDropCloth - cfg.frameSetCloth + cfg.frameCFSetCloth){
                ClothFall(false);
            }
            if ((_this_frame == cfg.frameEndCheckCF)) {
                PrepareEndOutData(towerList2, isolatedList2, clothSolver2, clothBlueprintCache2, forceRecorder2);
            }
        }
    }

    protected override void OnIterationEnd(){
        base.OnIterationEnd();
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

    Dictionary<string, object> _getOneSideConstructionData(List<Tower> towerList, List<Layer> isolatedList, int miss=-1){
        Dictionary<string, object> OnesideData = new(), towerListOut = new(), isoListOut = new();
        for(int i=0; i<towerList.Count; i++){
            if(miss == i) continue;
            Tower tower = towerList[i];
            towerListOut.Add(tower.name, tower.StateToAnnotation());
        }
        for( int i=0; i<isolatedList.Count; i++ ){
            if (miss == i + towerList.Count) continue;
            Layer iso = isolatedList[i];
            isoListOut.Add(iso.gameObject.name, iso.StateToAnnotation());
        }
        OnesideData.Add("towers", towerListOut);
        OnesideData.Add("isolatedObjects", isoListOut);
        return OnesideData;
    }

    Dictionary<string, object> _getCfExchange(bool isLeft, List<Tower> towerList, List<Layer> isolatedList, 
                                            List<(int, int)> exchangeCFList, List<(Vector3, Vector3)> ExchangeDist, int local_iter_n){
        Dictionary<string, object> temp = new();
        int A = exchangeCFList[local_iter_n].Item1, B = exchangeCFList[local_iter_n].Item2;
        temp.Add("exchangedIsLeft", isLeft);
        temp.Add("exchangeObjectA", A < towerList.Count ? towerList[A].name : isolatedList[A - towerList.Count].name);
        temp.Add("exchangeObjectAIsTower", A < towerList.Count);
        temp.Add("exchangeObjectAIdinCorresList", A % towerList.Count);
        temp.Add("movingVectorA", Vec2List(ExchangeDist[local_iter_n].Item1));
        temp.Add("exchangeObjectB", B < towerList.Count ? towerList[B].name : isolatedList[B - towerList.Count].name);
        temp.Add("exchangeObjectBIsTower", B < towerList.Count);
        temp.Add("exchangeObjectBIdinCorresList", B % towerList.Count);
        temp.Add("movingVectorB", Vec2List(ExchangeDist[local_iter_n].Item2));
        Dictionary<string, object> onesideListOut = _getOneSideConstructionData(towerList, isolatedList);
        Dictionary<string, object> allListOut = new();
        allListOut.Add(isLeft?"leftAll":"rightAll", onesideListOut);
        temp.Add("objectFullAnnotation", allListOut);
        return temp;
    }
    
    Dictionary<string, object> _getCfRemove(bool isLeft, List<Tower> towerList, List<Layer> isolatedList, List<int> removeCFList, int local_iter_n){
        Dictionary<string, object> temp = new();
        int removeId = removeCFList[local_iter_n];
        temp.Add("removedObject", removeId >= towerList.Count ? isolatedList[removeId - towerList.Count].name : towerList[removeId].name);
        temp.Add("removedIsLeft", isLeft);
        temp.Add("removedIsTower", removeId < towerList.Count);
        temp.Add("removedIdinCorresList", towerList.Count > 0? removeId % towerList.Count : removeId);
        Dictionary<string, object> onesideListOut = _getOneSideConstructionData(towerList, isolatedList, miss:removeId);
        Dictionary<string, object> allListOut = new();
        allListOut.Add(isLeft?"leftAll":"rightAll", onesideListOut);
        temp.Add("objectFullAnnotation", allListOut);
        return temp;
    }

    string _getFriction(string mat){
        if (mat == cfg.fric_high) return "HIGH";
        else if (mat == cfg.fric_med) return "MEDIUM";
        else if (mat == cfg.fric_low) return "LOW";
        else return mat;
    }


    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        if (!validity) {  return temp; }
        // mode
        temp.Add("mode", "");
        if (cfg.ANNO_MODE == cfg.FACTUAL){
            temp["mode"] = "FACTUAL_PREDICTIVE";
            // properties
            Dictionary<string, object> clothA = new(), clothB = new();
            clothA.Add("stretchingCompliance", cloth_stretch.Item1);
            clothA.Add("bendingCompliance", cloth_bend.Item2);
            clothA.Add("friction", _getFriction(cloth_friction_material));
            clothA.AddRange(forceRecorder.GetAll());
            clothB.Add("stretchingCompliance", cloth_stretch2.Item1);
            clothB.Add("bendingCompliance", cloth_bend2.Item2);
            clothB.Add("friction", _getFriction(cloth_friction_material2));
            clothB.AddRange(forceRecorder2.GetAll());
            temp.Add("clothLeft", clothA);
            temp.Add("clothRight", clothB);
            Dictionary<string, object> onesideListOut = _getOneSideConstructionData(towerList, isolatedList);
            Dictionary<string, object> onesideListOut2 = _getOneSideConstructionData(towerList2, isolatedList2);
            Dictionary<string, object> allListOut = new();
            allListOut.Add("leftAll", onesideListOut);
            allListOut.Add("rightAll", onesideListOut2);
            temp.Add("objectFullAnnotation", allListOut);
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange || cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange2){
            bool isLeft = cfg.ANNO_MODE == cfg.COUNTERFACTUAL_Exchange;
            temp["mode"] = "COUNTERFACTUAL_Exchange";
            if (isLeft) temp.AddRange(_getCfExchange(true, towerList, isolatedList, exchangeCFList, ExchangeDist, _this_cf_iter));
            else temp.AddRange(_getCfExchange(false, towerList2, isolatedList2, exchangeCFList2, ExchangeDist2, _this_cf_iter - this_exchange));
        }
        else if (cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower || cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower2){
            bool isLeft = cfg.ANNO_MODE == cfg.COUNTERFACTUAL_RemoveTower;
            temp["mode"] = "COUNTERFACTUAL_RemoveTower";
            if (isLeft) temp.AddRange(_getCfRemove(true, towerList, isolatedList, removeCFList, _this_cf_iter - this_exchange - this_exchange2));
            else temp.AddRange(_getCfRemove(false, towerList2, isolatedList2, removeCFList2, _this_cf_iter - this_exchange - this_exchange2 - this_removeTower));
        }
        return temp;
    }

    protected override void OnCounterFactualDone(){
        cfg.ANNO_MODE = cfg.FACTUAL;
    }
    
    protected override void OnValidFactualDone(){
        if (!cfg.debug_cf){
            this_exchange = exchangeCFList.Count;
            this_exchange2 = exchangeCFList2.Count;
            this_removeTower = removeCFList.Count;
            this_removeTower2 = removeCFList2.Count;
            _cf_iter_total = this_removeTower + this_exchange + this_removeTower2 + this_exchange2;
        }
        else{
            _cf_iter_total = 0;
        }
    }
    
    protected override void OnCounterFactualIterStart(){
        if (_this_cf_iter == 0 && this_exchange > 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL_Exchange;
        }
        else if (_this_cf_iter == this_exchange && this_exchange2 > 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL_Exchange2;
        }
        else if (_this_cf_iter == this_exchange + this_exchange2 && this_removeTower > 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL_RemoveTower;
        }
        else if (_this_cf_iter == this_exchange + this_exchange2 + this_removeTower && this_removeTower2 > 0){
            cfg.ANNO_MODE = cfg.COUNTERFACTUAL_RemoveTower2;
        }
        RefreshCache(towerList, isolatedList);// refresh the tower and iso cache for next iter
        RefreshCache(towerList2, isolatedList2);// refresh the tower and iso cache for next iter
    }
}
