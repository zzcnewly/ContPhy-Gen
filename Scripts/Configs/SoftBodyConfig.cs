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

namespace SoftBodyConfig{

// configs of the cloth collision scenario.
public class cfg{
    public const float voxel_absolute_size = 0.1f;
    public const int FACTUAL = 1, COUNTERFACTUAL = 2;
    public static int ANNO_MODE;
    public const int seed = 1231123;
    public const string pre_name = "soft_body";
    public const int numIterations = 9999999;
    public const int framesPerIteration = 2000;
    public const int captureFrames = 240;
    public const int stopCaptureTime = captureFrames;
    public const int startIteration = 0; // all decorated well and it's time to start camera capture and main collision
    public const float simDeltaTime = 0.0166f;
    public const int framesBetweenCaptures = 1;
    public const bool output_AnnoImage = true; 
    public const bool output_RGB = true;
    public const bool annotation_Visualization = false; 
    public const int frameRate = 30;
    public const int ValidIterNum = 2000; // iter number for each valid sample

    public static List<string> elasticityType = new List<string>(){"Elastic","Rigid","Plastic"};
    public static Dictionary<string, string> elasticityType2prefabPath = new Dictionary<string, string>(){{"Elastic", "Prefabs/Soft Body/Ball More Elastic"},
                                                                                               {"Rigid", "Prefabs/Soft Body/Ball Less Elastic"},
                                                                                               {"Plastic", "Prefabs/Soft Body/Ball Plastic"}};
    public static Dictionary<int, string> result_translate = new (){{1, "Left Pit Observed"},
                                                                    {2, "Right Pit Observed"},
                                                                    {3, "Left Pit to Predict"},
                                                                    {4, "Right Pit to Predict"},
                                                                    {5, "No Pit Observed or to Predict"}}; // 1: left pit observed, 2: right pit observed, 3: left pit to predict, 4: right pit to predict, 5: no pit observed or to predict
    // camera settings
    public static Vector3 lookat = new Vector3(0, 3.1f, 0f);
    public static Vector3 ballScale = new Vector3(1, 1, 1);
    public static List<float> cam_angle  = new List<float>(){- (float)(Math.PI) / 2 - 0.04f, - (float)(Math.PI) / 2 + 0.04f};//-1.65f, -1.50f}; 
    public static List<float> cam_angle2  = new List<float>(){(float)(Math.PI) / 10, (float)(Math.PI) / 6};
    public static List<float> cam_radius  = new List<float>(){16f, 16f, 16f, 0.3f};
    public static List<float> cam_fov  = new List<float>(){30f, 35f};

    public static List<double> container_size_range = new List<double>(){3.5, 5.5};
    public static List<double> ramp_w_size_range = new List<double>(){2.67, 3.3};
    public const float y_ramp_size = 4.11f, y_ramp_start = -0.07f, y_floor_piece = -0.5f, y_lower_wall_point = 1.2f;
    public const float y_ball_min = 5.11f, y_ball_max = 6.9f;
    public const float rest_width = 0.4f, hole_width = 1.2f, rest_width_floor_piece = 0.2f, rest_width_edge_leave = 1.2f;
    public const float min_wall_x_distance = 1.6f;
    public const float min_wall_x_length = 2.4f, max_wall_x_length = 3.7f;
    public const int color_wall = 9; // gray

    public const float wall_thickness_onZ = 1f, wall_thickness_onY=0.4f;
    public const float floor_thickness_onY = 1f, floor_thickness_onZ=1f;
    public static List<int> stick_clr_range = new List<int>(){11, 0, 10, 1, 2, 3, 4, 5, 6, 7, 8};// # MULTICHOICE: color WHITE, DEEP_RED, BLACK, ORANGE, YELLOW, GREEN, CYAN, BLUE, PURPLE, PINK, BROWN, GRAY
    
    public const string stick_prefab_path = "Prefabs/Soft Body/Stick";
    public const string solver_prefab_path = "Prefabs/Soft Body/Obi Solver";
    public const string light_prefab_path = "Prefabs/Soft Body/Directional Light";
    public const string bg_prefab_path = "Prefabs/Soft Body/Background";
    public const string bg2_prefab_path = "Prefabs/Soft Body/Foreground";
    public const string bg_name = "Background";
    public const string bg2_name = "Foreground";
    
    public const float light_x_min_deg = 20f, light_x_max_deg = 60f;
    public const float light_y_min_deg = -20f, light_y_max_deg = 20f;

}
}