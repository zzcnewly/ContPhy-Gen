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
public class MassRandomizerTag : RandomizerTag {
    public float mass;
    public float mass_multifier = 1f;
}


[Serializable]
[AddRandomizerMenu("Mass Randomizer")]
public class MassRandomizer : BaseRandomizer
{
    // 1. number, 2. mass, 3. speed, 4. camera view
    public FloatParameter mass = new(){ value = new UniformSampler(0.1f, 2) };
    public CategoricalParameter<float> mass_ = new(){};
    public bool categories_mode = false;
    protected override void OnAwake()
    {
        base.OnAwake();
        mass_.SetOptions(new List<float>(){0.2f});
    }
    protected override void OnIterationStart() {
        Sample_();
    }
    public void Sample_(){
        var tags = tagManager.Query<MassRandomizerTag>();
        List<float> mass_ls = new List<float>();
        float luck = UnityEngine.Random.Range(0f, 1f);
        int max_iter = 100, this_iter0 = 0;
        do {
            this_iter0++;
            mass_ls = new();
            foreach (var tag in tags) {
                float mass_temp = mass.Sample();
                if (categories_mode){
                    mass_temp = mass_.Sample();
                }
                mass_ls.Add(mass_temp);
            }
        } while (luck > 0.2f && this_iter0 < max_iter && mass_ls.Distinct().Count() < Mathf.Min(mass_ls.Count, 3));
        int this_obj0 = 0;
        foreach (var tag in tags) {
            float massx = mass_ls[this_obj0] * tag.mass_multifier;
            (tag.GetComponent(typeof(Rigidbody)) as Rigidbody).mass = massx;
            tag.mass = massx;
            this_obj0++;
        }
    }
    public override void SaveAll(string path_without_slash)
    {
        Dictionary<string, float> temp = new Dictionary<string, float>();
        foreach (var tag in tagManager.Query<MassRandomizerTag>()){
            temp.Add(tag.gameObject.name, tag.mass);
        }
        File.WriteAllText(path_without_slash + "/" + "output_mass.json", JsonConvert.SerializeObject(temp));
    }

    public override Dictionary<string, object> GetAll() {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        foreach (var tag in tagManager.Query<MassRandomizerTag>()){
            temp.Add(tag.gameObject.name, tag.mass);
        }
        Dictionary<string, object> res = new();
        res.Add("outputMass", temp);
        return res;
    }
}
