using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlacementConfig;
using Unity.VisualScripting;
using UnityEngine.Assertions.Must;
using UnityEngine.Rendering.UI;
using UnityEditor.PackageManager;
using UnityEditor;

namespace FluidSlidesConfig{

// configs of the cloth collision scenario.
public class cfg{
    public const bool debug = false, debug_cf = false;
    public const float voxel_absolute_size = 0.07f;
    public const int FACTUAL = 1, COUNTERFACTUAL = 2;
    public static int ANNO_MODE;
    public const int seed = 1231123;
    public const string pre_name = "fluid_slides";
    public const int numIterations = 9999999;
    public const int framesPerIteration = debug? 60:700;
    public const int captureFrames = debug? 60:500;
    public const int emitterPredStartTime = captureFrames  - 40;
    public const int stopCaptureTime = debug? 60:captureFrames ;
    public const int startIteration = 0; // all decorated well and it's time to start camera capture and main collision
    public const float simDeltaTime = 0.0166f;
    public const int framesBetweenCaptures = 1;
    public const bool output_AnnoImage = true; 
    public const bool output_RGB = true;
    public const bool annotation_Visualization = false; 
    public const int frameRate = 30;
    public const int ValidIterNum = 2000; // iter number for each valid sample
    public static bool addViscosityEffect = true;

    public const float gridSize = 0.06f;//0.08f;
    public const float particleSize = 0.02f;//0.0f;
    public const float simplifyquality = 0.5f;


    // camera settings
    public static Vector3 lookat = new Vector3(0, 1f, 0f);
    public static List<float> cam_angle  = new List<float>(){-1.65f, -1.50f}; //- (float)(Math.PI) / 2, - (float)(Math.PI) / 2};
    public static List<float> cam_angle2  = new List<float>(){-(float)(Math.PI) / 30, (float)(Math.PI) / 30};
    public static List<float> cam_radius  = new List<float>(){10f, 10f, 10f, 0.3f};
    public static List<float> cam_fov  = new List<float>(){50f, 60f};
    public const float field_paddings = 1f;
    public const float bg_pos_z = 0.5f;

    public static List<List<double>> container_size_range = 
    new List<List<double>>(){new List<double>(){5, 5}, 
                             new List<double>(){2.5, 2.5 }, 
                             new List<double>(){1, 1}}; //  x, y, z
    public static List<List<double>> container_loca_range = 
    new List<List<double>>(){new List<double>(){0.0, 0.0}, 
                             new List<double>(){0.0, 0.0}, 
                             new List<double>(){0.0, 0.0}}; //  x, y, z

    public const int sampleStickMaxNum = 7; // Change Hard mode:W / H + sampleStickMaxNum + sampleStickMaxTry + sampleStickDistMin
    public const int sampleStickMaxTry = 200;
    public const float sampleStickLenMin = 1.7f, sampleStickLenMax = 3f;
    public const float sampleStickDistMin = 0.7f, sampleStickDistMax = 999f;


    public const float rotational_stick_prob = 0.25f;
    public const float dist_wall_epsilon = 0.1f;

    public static List<double> sampleAngleCandidates = new List<double>(){-25, 25, -35, 35, -45, 45, -55, 55,}; //-65, 65,}; //90}; //  x, y, z

    public const float attempt_y_step = 0.05f, attempt_x_step = 0.2f;
    // public const float y_relative_jitter = 0.5f;

    public const string NORMAL = "Normal", BOILING = "Boiling";
    public const int NORMAL_ = 2, BOILING_ = 4;
    public const int GAS_ = 0, LIQUID_ = 1, GAS_DEAD_ = 4;
    public const int GAS_LIQUID = 0, LIQUID_GAS = 1;
    public static List<int> receptor_num_range = new List<int>(){2, 3, 4}; //  # unit length
    public const float receptor_w_relative = 0.6f, receptor_h_relative = 0.5f, cup_h_relative = 0.7f;
    public const float receptor_scale_stickReachIn = 0.15f;
    public const float receptor_scale_y = 0.15f, receptor_scale_z = 0.2f;
    public const float wall_thickness = 0.5f, stick_unpenetration_thickness_X=0.2f, stick_unpenetration_thickness_YUp=1.6f, stick_unpenetration_thickness_YDown=1.5f;
    public const float assign_stick_color_height_distinguish_thres = 0.7f;
    public static List<int> receptor_clr_range = new List<int>(){11, 0, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9};// # MULTICHOICE: color WHITE, DEEP_RED, BLACK, ORANGE, YELLOW, GREEN, CYAN, BLUE, PURPLE, PINK, BROWN, GRAY
    public static List<int> stick_clr_range = new List<int>(){11, 0, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9};// # MULTICHOICE: color WHITE, DEEP_RED, BLACK, ORANGE, YELLOW, GREEN, CYAN, BLUE, PURPLE, PINK, BROWN, GRAY
    // color properties
    public static List<int> fluid_clr_range = new List<int>(){1, 2, 3, 4, 5, 7, 12};// # MULTICHOICE: color ORANGE, YELLOW, GREEN, CYAN, BLUE, PINK, LIGHT_RED
    // NOTE: fluid_vis_types in one troal will never be all the same
    public static List<string> fluid_vis_types = new (){"Sticky", /*"Normal",*/ "Non-Sticky"};// # DISRANGE should append number
    public static Dictionary<string, double> fluid_vis_temp_change_range = new (){ 
                                                                        {"Non-Sticky",  vis_nonsticky_normal}, 
                                                                        {"Normal", vis_normal_normal}, 
                                                                        {"Sticky", vis_sticky_normal}};// # DISRANGE should append number
    public const float vis_nonsticky_normal = 0.02f;
	public const float vis_normal_normal = 1.3f;
	public const float vis_sticky_normal = 10f;
    
    public static List<double> fluid_sft_range = new List<double>(){1, 1};//{0.1, 1};// # DISRANGE should append number
    public static List<int> fluid_num_range = new List<int>(){3, 4}; //  # 2~4 CHOICE
    public static Dictionary<int, List<double>> fluid_rho_list = new (){{3, new List<double>(){1600, 3000, 4400}},
                                                                        {4, new List<double>(){1200, 2600, 4000, 5400}},
                                                                        /*{5, new List<double>(){200, 800, 1400, 2000, 2600}},
                                                                        {6, new List<double>(){200, 700, 1200, 1700, 2200, 2700}}*/}; //  # 2~4 CHOICE
    public static List<int> pred_fluid_num_range = new List<int>(){1, 2}; //  # 2~4 CHOICE
    public const int fluid_ptk_amount_total = 1500;
    public static Quaternion emitterRotation = Quaternion.Euler(90, 0, 0);
    public const float fluid_emitter_width = 0.5f;
    public static Vector3 emitterScale = new Vector3(fluid_emitter_width, 0.25f, 0.15f);
    public const string stick_prefab_path = "Prefabs/Fluid Slides/Stick";
    public const string rot_stick_prefab_path = "Prefabs/Fluid Slides/RotStick";
    public const string compensator_prefab_path = "Prefabs/Fluid Slides/CollisionCompensator";
    public const string receptor_prefab_path = "Prefabs/Fluid Slides/Receptor";
    public const string solver_prefab_path = "Prefabs/Fluid Slides/Solver";
    public const string emitter_prefab_path = "Prefabs/Fluid Slides/Emitter";
    public const string wall_hori_prefab_path = "Prefabs/Fluid Slides/WallHori";
    public const string wall_vert_prefab_path = "Prefabs/Fluid Slides/WallVert";
    public const string light_prefab_path = "Prefabs/Fluid Slides/Directional Light";
    public const string bg_prefab_path = "Prefabs/Fluid Slides/Background";
    public const string bg2_prefab_path = "Prefabs/Fluid Slides/Background2";
    public const string emission_mat_prefab_path = "Prefabs/Fluid Slides/MatFluid";
    
    public const float light_x_min_deg = -20f, light_x_max_deg = 10f;
    public const float light_y_min_deg = -50f, light_y_max_deg = 50f;


/////////////////////////////////////////////////////////////////////
    
    public static float fluid_emitter_speed = 0.7f;
    public static float fluid_emitter_random_v = 0.4f;
    public static float fluid_emitter_lifespan = 4000f;

    // fluid particle render details
    public static float render_particle_radius = 2f;
    public static bool show_particles = false;
    public static bool show_rendered_fluid = false;
    public static float fluid_color_apparency = 0.7f;
    
    // fluid renderer settings
    public static float thicknessCutoff = 0.31f;
    public static int thicknessDownsample = 2;
    public static float blurRadius = 0.0176f;
    public static int surfaceDownsample = 1;
    public static float smoothness = 0.101f;
    public static float ambientMultifier = 6f;
    public static float reflection = 0.0f;
    public static float metalness = 0.114f;
    public static float transparency = 0.0f;
    public static float absorption = 6.8f;
    public static float refraction = 0.004f;
    public static int refractionDownsample = 2;

    // fluid physics settings (rho and capacity are sampled)
    public static float fluid_resolution = 40;
    public static float fluid_smooth = 5;
    public static float buoyancy=-1;
    public static float atmosphericDrag=0.0f;
    public static float atmosphericPressure=0.0f;
    public static float vorticity=0.0f;
    public static float diffusion=0.0f;//0.05f;//0.0f;

	public const float vis_boiling_limit = 0.0f;
	public const float boiling_sft = -0.07f;
	public const float boiling_buoy = 2f;

    // coarse rendering settings
    public static Vector3 particleScale = 0.027f * new Vector3(1, 2, 1);
    public static float dyingLiquidScaleMul = 0.9f; // obsoleted

    // annotation
    public const int collide_particle_num_to_be_valid = 20;
    public const int receptor_particle_num_to_be_valid = 20;
    public const int sampled_particle_number = 100;
    public const int interval_particle_sample_output = (int)(fluid_ptk_amount_total / sampled_particle_number);
    public const int max_cf_remove = debug_cf ? 0 : 6;
    public const int min_pass_particles = 100;
    public static List<double> stick_icyboil_range = new List<double>(){0.00f, 0.00f};//0.08f, 0.13f}; // inpercentage // no icy/boil if 0.00-0.00
    public static List<string> stick_temperature = new List<string>() {NORMAL}; // normal is doubled
    public static List<string> stick_temperature_icy_boil = new List<string>() {BOILING};
}
}