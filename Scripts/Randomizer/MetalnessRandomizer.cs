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
public class MetalnessRandomizerTag : RandomizerTag {
}


[Serializable]
[AddRandomizerMenu("Metalness Randomizer")]
public class MetalnessRandomizer : BaseRandomizer
{
    // 1. number, 2. mass, 3. speed, 4. camera view
    public CategoricalParameter<float> candidate = new();

    protected override void OnIterationStart() {
        var tags = tagManager.Query<MetalnessRandomizerTag>();
        foreach (var tag in tags) {
            (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.SetFloat("_METALNESS", candidate.Sample());
        }
    }
    
    public override void SaveAll(string path_without_slash)
    {
        Dictionary<string, float> temp = new Dictionary<string, float>();
        foreach (var tag in tagManager.Query<MetalnessRandomizerTag>()){
            temp.Add(tag.gameObject.name, (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.GetFloat("_METALNESS"));
        }
        File.WriteAllText(path_without_slash + "/" + "output_metalness.json", JsonConvert.SerializeObject(temp));
    }

    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        foreach (var tag in tagManager.Query<MetalnessRandomizerTag>()){
            temp.Add(tag.gameObject.name, (tag.GetComponent<MeshRenderer>() as MeshRenderer).material.GetFloat("_METALNESS"));
        }
        Dictionary<string, object> res = new();
        res.Add("outputMetalness", temp);
        return res;     
    }
}
