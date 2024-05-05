
using System;
using System.Collections.Generic;
using System.Data;
using Unity.Burst.CompilerServices;
using UnityEngine.Perception.GroundTruth.Utilities;

namespace PulleyGroupConfig{

    class PulleyType{
    };
    
    class cfg{
        // Task / Question Mode
        public const int ValidIterNum = 2000; // iter number for each valid sample

        public const int RANDOM_MASS_MOTION = 1, BALANCE_TENSION = 2, MOVE_STH = 4, MASS_VARY_PREDICTIVE = 5;
        public const float voxel_absolute_size = 0.07f;
        public static int ANNO_MODE;
        public const int EQUAL_EVERY_MODE_NUM = 99999;
        public const int TRIAL_N_RANDOM_MASS_MOTION = EQUAL_EVERY_MODE_NUM;
        public const int FRAMES_RANDOM_MASS_MOTION = debug? 20:(version1_vid_allow_all? 300:400);
        public const int ADDITIONAL_TIME4PREDICTIVE = 200;
        public const bool debug = false, debug_cf = false; 
        
        // obsoleted /////////////////////////////////
        public const int TRIAL_N_BALANCE_TENSION = 0;
        public const int TRIAL_N_MASS_VARY_PREDICTIVE = 0;
        public const int FRAMES_MASS_VARY_PREDICTIVE = FRAMES_RANDOM_MASS_MOTION;
        public const int TRIAL_N_MOVE_STH = 0;
        public const int FRAMES_MOVE_STH = FRAMES_RANDOM_MASS_MOTION;
        //////////////////////////////////////////////
        
        public const int TotalIterNumber = TRIAL_N_BALANCE_TENSION + TRIAL_N_RANDOM_MASS_MOTION + TRIAL_N_MOVE_STH + TRIAL_N_MASS_VARY_PREDICTIVE;
        public const float simDeltaTime = 0.0166f;
        public const int framesBetweenCaptures = 1;
        public const int frameRate = 30;
        public const int stopReleaseFrameN = 8;
        // simulation settings
        public const string scene_name = "pulley_group";
        public const int framesPerIteration=800;
        public const int randomSeed =5834;
        public const int iterationCount = TotalIterNumber;
        public const int startIteration = 0; // all decorated well and it's time to start camera capture and main collision
        
        // for debug
        public const bool annotation_Visualization = false;
        public const bool output_AnnoImage = true; 
        
        public const int frame_collision_is_valid = 50, frame_collision_disks = 40;
        // Annotation threshold
        public const float rotation_threshold_degree = 35f; //35f;//60f;
        public const float motion_threshold = 1.2f; //1.5f;
        public const float angularVelocityThreshold = 20f;
        public const float stability_velocity_threshold = 1.8f;
        public const float stability_motion_threshold = 0.9f;

        // times
        public const int random_mass_motion_start_time = debug? 10:(version1_vid_allow_all ? 10:100); // mode 1 
        ///// Just for Display ////////////////////////////////
        public const int mass_change_start_time = random_mass_motion_start_time; // mode 5
        public const int move_sth_start_time = random_mass_motion_start_time; //mode 4
        public const int move_sth_flash_start_frame = 20;
        public const int move_sth_flash_period = 20;
        public const float moving_speed = 2f;
        public const float moving_max_distance = 3.5f; //6 is ok. check the distance and threshold
        ////////////////////////////////////////////////////////
        // load mass distribution
        public const float mass_const = 0.2f;
        public static List<float> load_mass_categories_range = new List<float>(){0.15f, 0.25f, 0.20f};
        public static List<float> change_mass_categories_range = new List<float>(){-0.1f};

        // other mode settings
        public const bool avoid_unstable_layout = true;
        public const bool keep_2_dyn_nodes_per_cable = true;
        public const int max_dyn_nodes_per_cable = 2;
        public const bool fix_orientation = true;
        public const bool enable_pulley_load_mass_doubled = true;
        public const float pulley_load_multifier = 1.975f;
        public const bool only_one_group = true;

        // Size Parameters
        public const float base_x = -20f, base_y = 20f;
        public const float y_step = 8f;
        public const float layer_step = 10f;
        public const float x_step = 3.7f; //5f
        public const float radius_scale = 0.8f;
        public const float ceil_floor_scale = 1.0f;
        public const float obj_pos_scale = 0.7f; // 0.5f
        // generative params
        public const int layer_num_min = 0, layer_num_max = 2; // 0,0
        public const int column_num_min_1layer = 3, column_num_max_1layer = 6; //7,8 //5,6
        public const int column_num_min_2layer = 2, column_num_max_2layer = 4;
        public const int element_num_min = 2, element_num_max = 5; // 2,5

        // Camera Parameters
        public const float look_at_x = base_x + 0.5f * column_num_max_1layer * x_step, look_at_y = base_y - layer_step * 0.5f * (layer_num_max+2), look_at_z = 0;
        public const float cam_lr_min = (float)(Math.PI) * 86 / 180;
        public const float cam_lr_max = (float)(Math.PI) * 94 / 180;
        public const float cam_lr_mean = (float)(Math.PI) / 2;
        public const float cam_lr_std = (float)(Math.PI) * 4 / 180;
        public const float cam_ud_min = -(float)(Math.PI) / 30;
        public const float cam_ud_max = (float)(Math.PI) / 30;
        public const float cam_ud_mean = 0;
        public const float cam_ud_std = (float)(Math.PI) / 30;
        public const float cam_dist_min = 33f, cam_dist_max = 36f, cam_dist_mean = 35f, cam_dist_std = 1f;
        public const float cam_fov_min = 40, cam_fov_max = 50;
        public const float field_paddings = 6.0f;
        // NODE STATES
        // Scale
        public const int BIG = 1, LITTLE = 0;
        // Pulley states
        public const int DYNAMIC_WAIT = -1;
        public const int DYNAMIC_WITH_LOAD = 0;
        public const int DYNAMIC_WITH_DYN = 1;
        public const int DYNAMIC_WITH_END = 2;
        public const int STATICS = 3;
        // Terminal states(may appear in states[][])
        public const int END_STATIC_CEIL = 4;
        public const int END_STATIC_FLOO = 5;
        public const int END_DYNAMIC_WAIT = 6;
        public const int END_DYNAMIC_LOAD = 7;
        public const int END_DYNAMIC_PULY = 8;
        public const int END_DYNAMIC_END = 9;
        public const int END_DYNAMIC_END_LOOP = 10;

        // Positions
        public const int UPPER = 0, LOWER = 1, UPPER_ = 2, LOWER_ = 3, UPPER_DYN = 4, LOWER_DYN = 5;

        // Dynamic nodes number in each rope
        public const int max_dyn_ele_n = 4;
        public const int min_dyn_ele_n = 2;

        // Probs
        public const float up_start_p = 0.25f; 
        public const float low_start_p = 0.25f;
        public const float dyn_start_p = 0.5f; // delete
        public const float sta_flat_p = 0.2f; // 1
        public const float low_dyn_p = 0.2f; // delete
        public const float low_sta_p = 0.1f; // 2
        public const float low_load_p = 0.0f; // 3
        public const float up_with_upper_dyn_across_p = 0.50f; // delete
        public const float up_with_upper_dyn_end_p = 0.45f;
        public const float up_with_upper_dyn_static_p = 0.05f;
        public const float low_end_sta_p  = 0.10f;
        public const float low_end_load_p = 0.00f;
        public const float low_end_wait_p = 0.90f; // delete

        // scale prob
        public const float big_pulley_p = 0.5f;

        // supporter scale
        public static UnityEngine.Vector3 supporter_scale = new UnityEngine.Vector3(1.0f, 0.9f, 1.0f);

        // other env settings
        public static float pulley_scale = 0.87f;// 1.0f; // determined by the relation of the collider radius vs. the cable disk radius
        public const string envroom_path = "Prefabs/PulleyGroup/Filo TestEnvironment1";
        public const string PPV_path = "Prefabs/PulleyGroup/Post Process Volume";
        public const string dirlight_path = "Prefabs/PulleyGroup/Directional Light";
        public const string dirlight_path1 = "Prefabs/PulleyGroup/Directional Light 1";
        public const string dirlight_path2 = "Prefabs/PulleyGroup/Directional Light 2";
        public const string dirlight_path3 = "Prefabs/PulleyGroup/Directional Light 3";
        public static UnityEngine.Vector3 bg_pos = new UnityEngine.Vector3(-7, 22, -1);
        public static UnityEngine.Vector3 bg_pos1 = new UnityEngine.Vector3(-7, 22, 1);
        public static UnityEngine.Vector3 light_pos = new UnityEngine.Vector3(-7, 22, -19);
        public static UnityEngine.Vector3 light_pos1 = new UnityEngine.Vector3(-7, 22, 19);
        public static UnityEngine.Quaternion light_rotation = UnityEngine.Quaternion.Euler(-1.7f, -0.7f, 0.4f);
        public static UnityEngine.Quaternion light_rotation1 = UnityEngine.Quaternion.Euler(-1.7f, -180f, 0.4f);
        public static UnityEngine.Vector3 light_pos_side = new UnityEngine.Vector3(17f,10f,-18f);
        public static UnityEngine.Vector3 light_pos1_side = new UnityEngine.Vector3(17f,10f,18f);
        public static UnityEngine.Quaternion light_rotation_side = UnityEngine.Quaternion.Euler(0,320f,0);
        public static UnityEngine.Quaternion light_rotation1_side = UnityEngine.Quaternion.Euler(0, 140f, 0);
        public const float ambientIntensity = 1000f;
        public static UnityEngine.Vector3 room_scale = new UnityEngine.Vector3(200f,1f,200f);
        public static UnityEngine.Vector3 room_pos = new UnityEngine.Vector3(26f,100f,2f);
        public static List<string> room_mat = new List<string>(){"Prefabs/PulleyGroup/Room1", "Prefabs/PulleyGroup/Room2"};
        public static List<int> load_color_range = new List<int>(){0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11};
        public static List<int> load_color_range_plus = new List<int>(){12,13,14,15,16};
        public static List<int> disk_color_range = new List<int>(){0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11};
        public static List<int> rope_color_range = new List<int>(){0, 2, 3, 4, 5, 6, 7, 10, 11};
        
        public const float particle_dist_thres = 0.7f;
        public const float sampling_dist = 0.5f;

        public const float uncorrect_rate = 0.0f;
        public const bool version1_vid_allow_all = false; // unable middle stop & check stability
        public const bool version2_vid_same_duration = true; // able middle stop & check stability and same duration
        public const bool version2_only_1dyn_group_static_be_moved = true; // able middle stop & check stability and same duration
        
        public const float insert_depth = 0.4f;
        public const float min_dist_between_objects = 4f;
        public const float random_layout_prob = 0.5f;
        public const float x_step_for_rearranged_group = 7f;
        public const int check_rope_repeat_element_time = 7, init_saving_rope_repeat_element_time = 9;

    }
}