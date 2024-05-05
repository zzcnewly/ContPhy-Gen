using UnityEngine;
using Obi;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.PlayerLoop;
using System.Runtime.InteropServices;
using UnityEditorInternal;
using UnityEngine.WSA;
using UnityEngine.Diagnostics;
using UnityEditor.Search;
using UnityEngine.Perception.GroundTruth;
using UnityEditor.VersionControl;
using UnityEngine.UIElements;
using Palmmedia.ReportGenerator.Core.Reporting.Builders;
using SamplingFunctions;
using FluidSlidesConfig;
using Unity.Profiling;
using System.Xml;
using PlacementConfig;
using GeometryIn2D;
using JetBrains.Annotations;
using UnityEditor.Rendering;
using System.Threading;
using UnityEngine.Animations;


public class CylinderIndicator : MonoBehaviour {
    public float State, Mass;
    public ObiSolver solver;
    public HeatTransport HT;
    public int id_k;
    public void Update(){
        Mass = solver.invMasses[id_k];
        State = HT.NowDataState[id_k]; 
    }
}

// assign under obiemitter
[RequireComponent(typeof(ObiEmitter))]
public class MakeCylinders : MonoBehaviour
{
    public int color = 0; // should be assigned in the main code
    public ObiSolver solver; // should be assigned in the main code
    Material material;
    public static bool enable = true;
    Vector3 scale = cfg.particleScale;
    float dyingScale = cfg.dyingLiquidScaleMul;
    ObiEmitter actor;
    List<GameObject> goList = new();
    public HeatTransport HT;
    public void Start()
    {
        material = Material.Instantiate(Resources.Load(cfg.emission_mat_prefab_path) as Material);
        goList = new();
        actor = GetComponent<ObiEmitter>();
    }

    public void OnDestroy()
    {
        foreach(GameObject go in goList){
            GameObject.DestroyImmediate(go);
        }
        goList = new();
    }

    public GameObject CreateCylinder(Vector3 pos, Vector3 scale, int color){
        GameObject new_p_obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        new_p_obj.transform.position = pos;
        var new_scale = new Vector3(scale.x, scale.y * (UnityEngine.Random.Range(0.99f, 1.01f)), scale.z);
        new_p_obj.transform.localScale = new_scale;
        new_p_obj.transform.rotation = Quaternion.Euler(90, 0, 0);
        new_p_obj.GetComponent<Collider>().enabled = false;
        new_p_obj.GetComponent<Renderer>().material.color = SoftColor.color_rgb_dict[color];
        new_p_obj.transform.parent = actor.transform;
        Color c = SoftColor.color_rgb_dict[color];
        material.color = c;
        material.SetColor("EmissiveColor", c);
        new_p_obj.GetComponent<MeshRenderer>().material = material;
        return new_p_obj;
    }

    void _AddIndicator(int id_k, GameObject go){
        var ti = go.AddComponent<CylinderIndicator>();
        ti.HT = HT;
        ti.solver = solver;
        ti.id_k = id_k;
    }

    public void Update(){
        if(!enable) return;
        int len = actor.solverIndices.Length;
        int toMove = goList.Count;
        int toCrea = len - goList.Count;
        var HTState = HT.NowDataState;
        if (toCrea < 0){
            for (int i=0; i<-toCrea; i++){
                GameObject.DestroyImmediate(goList[goList.Count-1]);
                goList.RemoveAt(goList.Count-1);
            }
            toCrea = 0;
        }
        for (int i=0; i<toMove; i++){
            int idx = actor.solverIndices[i];
            if(cfg.debug){
                goList[i].transform.position = solver.positions[idx];
            }
            else{
                if (HTState[idx] != cfg.LIQUID_){            
                    goList[i].SetActive(false);
                }
                else {
                    goList[i].transform.position = solver.positions[idx];
                }
            }
        }
        for (int i=0; i < toCrea; i++){
            int k = actor.solverIndices[i+toMove];
            Vector4 pos = solver.positions[k];
            GameObject newpObj = CreateCylinder(pos, scale, color);
            if(cfg.debug) _AddIndicator(k, newpObj);
            goList.Add(newpObj);
        }
    }

}
