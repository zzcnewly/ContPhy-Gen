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



[RequireComponent(typeof(Rigidbody))]
public class NormalRandomizerTag : RandomizerTag {
}


[Serializable]
[AddRandomizerMenu("Normal Randomizer")]
public class NormalRandomizer : BaseRandomizer
{
    public CategoricalParameter<string> candidate = new();
    Dictionary<string, string> temp;
    protected override void OnIterationStart() {
        var tags = tagManager.Query<NormalRandomizerTag>();
        temp = new ();
        foreach (var tag in tags) {
            string name = candidate.Sample();
            (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.SetTexture("_NormalMap", Resources.Load<Texture2D>(name));
        }
    }
    
    public override void SaveAll(string path_without_slash)
    {
    }

    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        return temp;
    }
}
