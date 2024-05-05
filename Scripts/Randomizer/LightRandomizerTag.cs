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
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.UIElements;
using System.Runtime.InteropServices;
using UnityEngine.PlayerLoop;
using UnityEditor.PackageManager.UI;
using ClothCollisionConfig;

[Serializable]
[AddRandomizerMenu("Light Randomizer")]
public class LightRandomizer : BaseRandomizer
{
    public GameObject light; // assigned outside the class
    List<float> rotEuler;
    public FloatParameter cam_angle_x;
    public FloatParameter cam_angle_y;
    protected override void OnScenarioStart() {
        base.OnScenarioStart();
    }
    
    protected override void OnIterationStart() {
        base.OnIterationStart();
        rotEuler = new (){cam_angle_x.Sample(), cam_angle_y.Sample(), 0};
        light.transform.rotation = Quaternion.Euler(new Vector3(rotEuler[0], rotEuler[1], rotEuler[2]));
    }

    public override void SaveAll(string path_without_slash) {}

    public override Dictionary<string, object> GetAll()
    {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        temp.Add(light.name, new Dictionary<string, object>());
        (temp[light.name] as Dictionary<string, object>).Add("rotation", rotEuler);
        Dictionary<string, object> res = new();
        res.Add("outputLight", temp);
        return res;
    }

}
