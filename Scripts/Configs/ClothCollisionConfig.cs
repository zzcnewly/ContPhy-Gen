using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using PlacementConfig;
using Unity.VisualScripting;
using UnityEngine;

namespace ClothCollisionConfig{

// configs of the cloth collision scenario.
class cfg{
    public const string scene_name = "cloth_collision";
    public const bool debug = false, debug_cf = true, debug_force = false;
    public const float voxel_absolute_size = 0.14f;
    public const int FACTUAL = 1, COUNTERFACTUAL_Exchange=2, COUNTERFACTUAL_RemoveTower=3, COUNTERFACTUAL_Exchange2=4, COUNTERFACTUAL_RemoveTower2=5;
    public static int ANNO_MODE;
    public const int framesPerIteration = debug?30:290;
    public const int frameSetCloth = debug?20:60, frameCFSetCloth = 10; // frameSetCloth % 10 == 0
    public const int frameDropCloth = debug?25:190; // frameSetCloth % 10 == 0
    public const int frameEndCheckFT = framesPerIteration - 2;
    public const int frameEndCheckCF = framesPerIteration - frameSetCloth + frameCFSetCloth - 2;
    public const int randomSeed = 232456;
    public const int iterationCount = 9999999;
    public const int startIteration = 0; // all decorated well and it's time to start camera capture and main collision
    public const float simDeltaTime = 0.0166f;
    public const int framesBetweenCaptures = 1;
    public const bool output_AnnoImage = true; 
    public const bool output_RGB = true;
    public const bool annotation_Visualization = false; 
    public const int frameRate = 30;
    public const int ValidIterNum = 2000; // iter number for each valid sample
    
    // Room Placement settings
    public static Vector3 room_pos = new Vector3(0, -0.5f, 0);
    public static Quaternion room_rotation = Quaternion.Euler(-0, 0, 0);
    public static Vector3 room_scale = new Vector3(40, 1, 40);
    public static Vector3 table_pos = new Vector3(-0.2138257f, -1.71676f, -0.1020876f);
    public static Quaternion table_rotation = Quaternion.Euler(0, 180, 0);
    public static Vector3 table_scale = new Vector3(8.40941f, 3.4036f, 4.82767f);
    // camera settings
    public static Vector3 camera_pos = new Vector3(0, 0, 0);
    public const string dirlight_path = "Prefabs/Cloth Collision Blocks/Cloth/Directional Light";
    public static UnityEngine.Vector3 light_pos = new UnityEngine.Vector3(0,20,0);
    public static UnityEngine.Quaternion light_rotation = UnityEngine.Quaternion.Euler(37.9730721f,359.160004f,359.932312f);
    
    // sdf setting
    public const int cloth_substeps = 5;// 4, 10
    public const float sleepThreshold = 0.001f; // 0.001f, 0.00015f
    public const float sleepThresholdSettled = 0.001f; // 0.001f
    public const float damping = 0.01f; // 0.155f
    public const int maxDepenetration = 1; // 5

    // friction settings
    public const string fric_high = "Prefabs/Cloth Collision Blocks/Cloth/HighFriction";
    public const string fric_low = "Prefabs/Cloth Collision Blocks/Cloth/LowFriction";
    public const string fric_med = "Prefabs/Cloth Collision Blocks/Cloth/MediumFriction";
    public const string rsc_prefix = "Prefabs/Cloth Collision Blocks/Cloth/";
    public static List<string> friction_mat_range = new List<string>(){ rsc_prefix + "LowFriction", rsc_prefix + "HighFriction"};//, rsc_prefix + "MediumFriction"};
    // high fric mass distribution: 3, 6, 15, 60, 90?
    // low fric : 0.4, 1, 2,3,4,5, 12, 24

    public static List<(int, float)> iso_shape_candidate = new (){(12, 1.0f)};// {1, 2, 11}; // 1 cube, 2 sphere, 11 cubiod
    public static List<float> mass_candidates_highF_tower = new (){ 99999, 12f, 12.002f, 12.001f,}; // first must be lighter mass value
    public static List<float> mass_candidates_lowF_tower = new (){99999, 6.5001f, 6.5002f, 6.5f }; // first must be lighter mass value
    public static List<float> mass_candidates_highF = new (){4, 4.001f, 4.002f, 99999, 99998}; // first must be lighter mass value
    public static List<float> mass_candidates_lowF = new (){2f, 2.001f, 2.002f, 99999, 99998}; // first must be lighter mass value
    public const float two_sets_x_interval = 2.4f;
    public const float top_sphere_prob = 0.0f;//0.4f;
    public const float max_tower_height = 2.2f;
    public const float cubiod_height_min = 1.4f, cubiod_height_max = 2.1f;
    public const float cuboid_scale_z_min = 0.35f, cuboid_scale_z_max = 0.45f;
    public const float cuboid_scale_x_min = 0.9f, cuboid_scale_x_max = 1.4f;
    public const float cube_scale_min = 0.45f, cube_scale_max = 0.6f;
    public const float max_tolerant_starting_height_moving = 0.4f;
    public const float delete_rate_compare_to_merge = 0.0f;
    public const float support_area_squeeze_rate = 0.6f;
    public static List<int> tower_layer_num = new List<int>() {1};
    public const int tower_num_min = 1, tower_num_max = 2; 
    public const int iso_num_min = 1, iso_num_max = 2;

    public const float pos_x_range = 0.8f, pos_x_range_inner = -0.8f; 
    public const float pos_x_min = -pos_x_range, pos_x_max = -pos_x_range_inner, pos_z_min = -0.6f, pos_z_max = 0.4f; 
    public const float pos_x_min2 = pos_x_range_inner, pos_x_max2 = pos_x_range, pos_z_min2 = pos_z_min, pos_z_max2 = pos_z_max; 
    public const float pos_x_min_tower = -0.3f, pos_x_max_tower = 0.3f, pos_z_min_tower = -1.6f, pos_z_max_tower = -0.6f; 

    public const float cloth_height_addition_min = 0f, cloth_height_addition_max = 0.3f; 
    public const int upper_limit_try_time = 1000;
    public const float observable_taller_thres_in_h_rate = 0.8f;

    public const int static_judge_frames = 15;
    public const float static_thres = 0.2f;
    public const float field_paddings = 0f;

    public const float obj_rotation_y_min = 1f, obj_rotation_y_max = 359f;
    public const float plate_rotation_y_min = -15f, plate_rotation_y_max = 30f;
    public const int color_patient = 0; //red
    public const float mass_patient = 99999; //red
    public const string name_patient = "Red Rod"; //red
    public static List<int> cube_color_range = new List<int>(){1, 2, 3, 4, 5, 6, 7, 8, 9, 11};
    public static List<int> cube_color_range_plus = new List<int>(){12,13,14,15,16};
    
    // cloth
    public const float placement_thickness = -0.001f;//-0.012f;//-0.001f;
    public const string cloth_path = "Prefabs/Cloth Collision Blocks/Cloth/Solver";
    public const string cloth_blueprint_path1 = "Prefabs/Cloth Collision Blocks/Cloth/Cloth Blueprint 1";
    public const string cloth_blueprint_path2 = "Prefabs/Cloth Collision Blocks/Cloth/Cloth Blueprint 2";
    public static Vector3 cloth_pos = new Vector3(0f, max_tower_height-2f, -3.4f);
    public const float move_z_length = 4.8f, random_z_additional_length_min = -0.1f, random_z_additional_length_max = 0.5f, random_speed_additional = 0.0f;
    public static Quaternion cloth_rotation = Quaternion.Euler(0, 0, 0);
    public static List<float> stretchingScale = new List<float>(){1.0f, 1.0f, 1.0f};
    public static List<((float, float), float)> stretchPro = new List<((float, float), float)>(){((0.011f, /*0.6f*/0.0f), 0.5f), ((0.0f, 0.0f), 0.5f)}; 

    public static List<((float, float), float)> bendPro = new List<((float, float), float)>(){((0f, 0.013f), 0.5f), ((0.0f, 0.035f), 0.5f)}; 
    // (bendCompliance, maxBending)

    public const float look_at_x = 0, look_at_y = max_tower_height / 2, look_at_z = 0;
    public const float cam_lr_min = -(float)(Math.PI) * 90 / 180, cam_lr_max = -(float)(Math.PI) * 90 / 180, cam_lr_mean = -(float)(Math.PI) / 2, cam_lr_std = (float)(Math.PI) * 5 / 180;
    public const float cam_ud_min = (float)(Math.PI) / 6 , cam_ud_max = (float)(Math.PI) / 6, cam_ud_mean = (float)(Math.PI) / 6, cam_ud_std = (float)(Math.PI) / 20;
    public const float cam_dist_min = 10f, cam_dist_max = 12f, cam_dist_mean = 11f, cam_dist_std = 1f;
    public const float cam_fov_min = 30, cam_fov_max = 40;
    public const float rotationSpeed = 0.8f; // speed of the rotation
    public const float jitterAmount = 0.0f; // amount of the jitter
    public const float rotationDuration = 2.0f; // rotation duration in seconds
    public const float rotationY = 0.0f;
    public const float angleLimit = 8f;
    
    public const string material_candidates_path = "Prefabs/Cloth Collision Blocks/Cloth/Materials/cloth_material/mat ";
    public const int material_candidates_num = 1;
    
    public const string UPRIGHT = "Upright", RECLINING = "Reclining", SUPINE = "Supine", SWOOPING = "Swooping", OTHER = "Other";
    public const float supine_thres = 0.02f, leaning_thres=0.02f;
    
} 

}