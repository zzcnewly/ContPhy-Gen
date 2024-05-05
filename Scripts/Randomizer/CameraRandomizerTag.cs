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

[RequireComponent(typeof(Camera))]
public class CameraRandomizerTag : RandomizerTag {
    public List<float> position;
    public List<float> rotation;
    public List<float> fov;
}


[Serializable]
[AddRandomizerMenu("Camera Randomizer")]
public class CameraRandomizer : BaseRandomizer
{
    public Vector3 lookat = new Vector3(1.303f, 2.342f, 0.163f);
    public FloatParameter cam_angle  = new() { value = new UniformSampler(0f, 2 * (float)(Math.PI)) };
    public FloatParameter cam_angle2  = new() { value = new NormalSampler(-(float)(Math.PI) / 2, (float)(Math.PI) / 2, 0, (float)(Math.PI) / 6) };
    public FloatParameter cam_radius  = new() { value = new NormalSampler(2.5f, 5f, 3, 0.5f, true, 2.5f, 5f) };
    public FloatParameter cam_fov  = new() { value = new UniformSampler(72, 100) };
    GameObject _cam;
    public bool mirror = false;
    public bool mirrored = false;
    public FloatParameter rand01  = new() { value = new UniformSampler(0, 1) };
    
    protected override void OnIterationStart() {
        SampleCamera();
    }

    public void SampleCamera(){
        var tags = tagManager.Query<CameraRandomizerTag>();
        foreach (var tag in tags) {
            var radius = cam_radius.Sample();
            var angle2 = cam_angle2.Sample();
            var angle = cam_angle.Sample();
            mirrored = false;
            if (mirror && rand01.Sample() > 0.5) {
                angle = (float)(Math.PI) + angle;
                mirrored = true;
            }
            var fov = cam_fov.Sample();
            var y =  (float)(lookat.y + radius * Math.Sin((double)(angle2)));
            var r_xz = radius * Math.Cos((double)(angle2));
            var x = (float)(lookat.x + r_xz * Math.Cos((double)(angle)));
            var z = (float)(lookat.z + r_xz * Math.Sin((double)(angle)));
            (tag.GetComponent(typeof(Camera)) as Camera).fieldOfView = fov;
            tag.transform.position = new Vector3(x, y, z);
            tag.transform.rotation = Quaternion.identity;
            tag.transform.Rotate(new Vector3(0, 1, 0), -90-angle * 180 / (float)(Math.PI), Space.Self);
            tag.transform.Rotate(new Vector3(1, 0, 0), angle2 * 180 / (float)(Math.PI), Space.Self);
            Vector3 temp = tag.transform.rotation.eulerAngles;
            tag.position = new List<float>(new float[] {x, y, z});
            tag.rotation = new List<float>(new float[] {temp.x, temp.y, temp.z});
            tag.fov = new List<float>(new float[] {fov});
            _cam = tag.gameObject;
            break;
        }
    }

    public void EncapsulateObjects(List<GameObject> objectsToInclude, Camera mainCamera, float padding)
    {
        if (objectsToInclude.Count == 0) return;

        Bounds bounds = CalculateBounds(objectsToInclude);
        Vector3 center = bounds.center;
        Vector3 cameraPosition = CalculateCameraPosition(bounds, mainCamera, mainCamera.fieldOfView, padding);

        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.LookAt(center);
        Vector3 temp = mainCamera.transform.rotation.eulerAngles;
        var tag = mainCamera.GetComponent<CameraRandomizerTag>();
        tag.position = new List<float>(new float[] {cameraPosition.x, cameraPosition.y, cameraPosition.z});
        tag.rotation = new List<float>(new float[] {temp.x, temp.y, temp.z});
    }

    public void MoveCameraPosition(Vector3 pos, Camera mainCamera){
        var tag = mainCamera.GetComponent<CameraRandomizerTag>();
        mainCamera.transform.position = pos;
        tag.position = new List<float>(new float[] {pos.x, pos.y, pos.z});
    }

    Bounds CalculateBounds(List<GameObject> objects)
    {
        Bounds bounds = new Bounds(objects[0].transform.position, Vector3.zero);

        for (int i = 1; i < objects.Count; i++)
        {
            bounds.Encapsulate(objects[i].GetComponent<Renderer>().bounds);
        }

        return bounds;
    }

    Vector3 CalculateCameraPosition(Bounds bounds, Camera mainCamera, float cameraFOV, float padding)
    {
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y);
        float distance = maxDimension * 0.5f / Mathf.Tan(cameraFOV * 0.5f * Mathf.Deg2Rad);

        Vector3 direction = mainCamera.transform.position - bounds.center;
        direction.Normalize();

        return bounds.center + direction * (distance + padding);
    }


    public override void SaveAll(string path_without_slash)
    {
        Dictionary<string, Dictionary<string, List<float>>> temp = new Dictionary<string, Dictionary<string, List<float>>>();
        foreach (var tag in tagManager.Query<CameraRandomizerTag>()){
            temp.Add(tag.gameObject.name, new Dictionary<string, List<float>>());
            temp[tag.gameObject.name].Add("position", tag.position);
            temp[tag.gameObject.name].Add("rotation", tag.rotation);
            temp[tag.gameObject.name].Add("fov", tag.fov);
            temp[tag.gameObject.name].Add("mirrored", new List<float>(new float[] {mirrored ? 1 : 0}));
        }
        File.WriteAllText(path_without_slash + "/" + "output_camera.json", JsonConvert.SerializeObject(temp));
    }
    
    public override Dictionary<string, object> GetAll()
    {
        Dictionary<string, object> temp = new Dictionary<string, object>();
        var tag = _cam.GetComponent<CameraRandomizerTag>();
        temp.Add(_cam.name, new Dictionary<string, object>());
        (temp[_cam.name] as Dictionary<string, object>).Add("position", tag.position);
        (temp[_cam.name] as Dictionary<string, object>).Add("rotation", tag.rotation);
        (temp[_cam.name] as Dictionary<string, object>).Add("fov", tag.fov);
        (temp[_cam.name] as Dictionary<string, object>).Add("mirrored", mirrored ? 1 : 0);
        Dictionary<string, object> res = new();
        res.Add("outputCamera", temp);
        return res;
    }

}
