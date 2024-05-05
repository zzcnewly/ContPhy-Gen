using UnityEngine;
using System.Collections;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using System;
using System.Collections.Generic;
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
using Filo;
using UnityEngine.Rendering;
using System.Numerics;
using UnityEditor.Experimental.GraphView;
using JetBrains.Annotations;
using UnityEditor.Animations;
using Palmmedia.ReportGenerator.Core.Reporting.Builders;
using Unity.Collections;
using UnityEditor.Rendering.LookDev;
using Unity.Services.Core.Environments;
using System.Security.Cryptography;
using System.Globalization;
using UnityEditorInternal.Profiling.Memory.Experimental;
using PulleyGroupConfig;
using PlacementConfig;
using UnityEditor.UI;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Profiling;
using UnityEngine.Perception.Randomization.Scenarios;

public class CollisionStopIter : MonoBehaviour
{
    public PulleyGroupManager _manager;
    public bool isLoad;
    void Start()
    {   
        isLoad = !(name.Contains("Pulley") || name.Contains("Static") || name.Contains("Fixed"));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isLoad) {
            // cube collide on a disk
            if (collision.collider.gameObject.GetComponent(typeof(CableDisc)) != null) {
                bool valid = true;
                if (_manager._this_frame - cfg.random_mass_motion_start_time < cfg.frame_collision_is_valid) valid = false;
                if ((!cfg.version2_vid_same_duration) || cfg.ANNO_MODE != cfg.RANDOM_MASS_MOTION) _manager.StopThisIterImmediatelyAndInvalidIt(valid);
            }
        }
        else {
            // dyn disk collide on a static disk
            if (_manager._this_frame > cfg.random_mass_motion_start_time + cfg.frame_collision_disks){
                if (collision.collider.gameObject.GetComponent(typeof(CableDisc)) != null) {
                        if ((!cfg.version2_vid_same_duration) || cfg.ANNO_MODE != cfg.RANDOM_MASS_MOTION) _manager.StopThisIterImmediatelyAndInvalidIt(true);
                }
            }
        }
    }
}