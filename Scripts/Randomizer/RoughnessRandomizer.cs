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
public class RoughnessRandomizerTag : RandomizerTag {
}


[Serializable]
[AddRandomizerMenu("Roughness Randomizer")]
public class RoughnessRandomizer : BaseRandomizer
{
    public CategoricalParameter<float> candidate = new();

    protected override void OnIterationStart() {
        var tags = tagManager.Query<RoughnessRandomizerTag>();
        foreach (var tag in tags) {
            (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.SetFloat("_REFLECTIONS_ROUGHNESS", candidate.Sample());
        }
    }
    
    public override void SaveAll(string path_without_slash)
    {
        Dictionary<string, float> temp = new Dictionary<string, float>();
        foreach (var tag in tagManager.Query<RoughnessRandomizerTag>()){
            temp.Add(tag.gameObject.name, (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.GetFloat("_REFLECTIONS_ROUGHNESS"));
        }
        File.WriteAllText(path_without_slash + "/" + "output_roughness.json", JsonConvert.SerializeObject(temp));
    }
    
    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        foreach (var tag in tagManager.Query<RoughnessRandomizerTag>()){
            temp.Add(tag.gameObject.name, (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.GetFloat("_REFLECTIONS_ROUGHNESS"));
        }
        Dictionary<string, object> res = new();
        res.Add("outputRoughness", temp);
        return res;
    }
}
