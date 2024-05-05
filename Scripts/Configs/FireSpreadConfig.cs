using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;

namespace FireSpreadConfig{

// configs of the cloth collision scenario.
class cfg{
   
    // mode settings
    public const int FACTUAL = 1, PREDICTIVE = 2, COUNTERFACTUAL_UNABLE = 3, COUNTERFACTUAL_CHANGETRIGGER = 4;
    public static int ANNO_MODE;
    public const int EQUAL_EVERY_MODE_NUM = 500;
    public const int TRIAL_N_FACTUAL = 0;
    public const int TRIAL_N_PREDICTIVE = EQUAL_EVERY_MODE_NUM;
    public const bool debug_mode = false;

    // simulation settings
    public const string scene_name = "fire_spread";
    public const float simDeltaTime = 0.0166f;
    public const int framesBetweenCaptures = 1;
    public const int framesPerIteration = 10000;
    public const int randomSeed = 554376550;//90056;
    public const int iterationCount = 9999999;
    public const int startIteration = 0; // all decorated well and it's time to start camera capture and main collision
    public const int IgniteTime = 300; // drop the trigger
    public const int WaitTime = 100; 
    public const int PrepareDropTime = 100, MaxDropTime = 100, MinBurningTime = 200, MaxPermitBurningTime = 1300, BridgeCrawlTime = 1500;
    public const int MaxPermitRestTime = 1000;
    public const int frameRate = 30;
    public const int ValidIterNum = 2000; // iter number for each valid sample
    

    public const bool output_AnnoImage = true; 
    public const float cam_angle_min = 2.98f * (float)Math.PI / 2, cam_angle_max = 3.02f * (float)Math.PI / 2; // unit: degrees
    public const float cam_angle2_min = (float)Math.PI / 2 * 4 / 9, cam_angle2_max = (float)Math.PI / 2 * 5 / 9; // unit: degrees
    public const float cam_radius_min = 300f, cam_radius_max = 300f; // note: not real radius. unit: meters
    public const float CamFinalDist = 28.73f; // notr: real radius finally. unit: meters
    public const float cam_fov_min = 60f, cam_fov_max = 62f; // unit: meters
    public static Vector3 cam_lookat = new Vector3(0, 0, 0);
    public const float field_paddings = -0f;

    // wood placement settings 
    public static UnityEngine.Vector2 placementArea = new UnityEngine.Vector2(6, 6); 
    public const float seperationDistance = 2f; 
    public const float obj_x_scale_min = 15f, obj_x_scale_max = 25f;
    public const float obj_y_scale = 0.8f, obj_z_scale = obj_y_scale;
    public const float trigger_scale = 0.6f;
    public const float first_wood_height = 1.5f;
    public const float other_wood_height_step = 2f;
    public const float trigger_height = 1.5f;
    public const string trigger_name = "trigger";
    public const float obj_rotation_x_min = 0f, obj_rotation_x_max = 0f; // unit: degrees
    public const float obj_rotation_y_min = 1f, obj_rotation_y_max = 359f;
    public const float obj_rotation_z_min = 0, obj_rotation_z_max = 0f;
    public const int wood_count_min = 2, wood_count_max = 4;//5; // max number actual num maybe less according to disk sampling
    public static List<int> wood_color_range = new List<int>(){0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11};
    public const string trigger_prefab_path = "Prefabs/FireSpread/Trigger";
    public const string wood_prefab_path = "Prefabs/FireSpread/Wood";

    // Room settings
    public const string room_name = "Floor";
    public const string room_prefab_path = "Prefabs/FireSpread/Ground";
    public const string flame_engine_path = "Prefabs/FireSpread/FlameEngine";
    public const string postprocess_path = "Prefabs/FireSpread/Post Process Volume";
    public const string reflecprob_path = "Prefabs/FireSpread/Reflection Probe";
    public const string dirlight_path = "Prefabs/FireSpread/Directional Light";
    public static Vector3 room_pos = new Vector3(0, 0, 0);
    public static Quaternion room_rotation = Quaternion.Euler(-0, 0, 0);
    public static Vector3 light_pos = new Vector3(18.67f, 0, -9.8f);
    public static Quaternion light_rotation = Quaternion.Euler(0,0,0);//Quaternion.Euler(91, -124, -151);
    // fire params configs
    public static List<float> fireCrawlSpeed = new List<float>(){3f, 7f}; // {3f, 10f}0426
    public static List<float> fireDurationTime = new List<float>(){9999f};//{4f, 7f}; // {5f, 8f}0426
    public const float unflammable_rate = 0.3f;
    public static Vector3 fireBboxSpaceAddition = 1f*(new Vector3(0.02f, 0.02f, 0.02f));
    public const float maxSpreadUnflammable = 0.1f, maxDuration = 0.1f;


    // bridge settings
    public const float bridge_scale_x = 38f;
    public const float bridge_durationTime = 100f;
    public const float bridge_fireCrawlSpeed = 3f, bridge_fireCrawlSpeedSlow = 0.6f;//0.2f;
    public const float campFire_left_x_offset = -12f;
    public const float campFire_right_x_offset = +12f;
    public static UnityEngine.Vector2 placementAreaRiCamp = new UnityEngine.Vector2(6, 7); 
    public const float seperationDistanceRiCamp = 1.5f;

    public const float triggerSpareSpaceThreshold = obj_y_scale;

    public const bool annotation_Visualization = false; 

    public const int static_judge_frames = 15;
    public const float static_thres = 0.001f;

    public const float describeXTooCloseThreshold = 5f;

    public const int max_sample_particle = 8;

    public const float colorIntensityMultiplier = 0.07f;
    public static Color fireColorNew = new Color(0.8301887f, 0.4963863f, 0.02662864f, 1);
    public const float fireColorBlend = 1f;
    public const float flameLength = 2.5f;
    public const float flameVFXMultiplier = 5.4f;
    public const float flameEnvironmentalSpeed = 2.39f;
    public const float flameLiveliness = 4.62f;
    public const float flameLivelinessSpeed = 0.72f;
    public const float flameParticleSize = 3f;
} 

}