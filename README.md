# ContPhy-Gen Codebase & Tutorial


Codebase for ContPhy dataset generation. 

> ### ContPhy: Continuum Physical Concept Learning and Reasoning from Videos   
> Zhicheng Zheng*, Xin Yan*, Zhenfang Chen*, Jingzhou Wang, Qin Zhi Eddie Lim, Joshua B. Tenenbaum, and Chuang Gan (* denotes equal contributions)  
>
> *ICML 2024*  
> Links | [Project Page](https://physical-reasoning-project.github.io/) | [Paper (Arxiv)](https://arxiv.org/abs/2402.06119) | [Dataset Download](https://huggingface.co/datasets/zzcnewly/ContPhy_Dataset) | [Cite ContPhy](#citation)
>
> **News 2024/08/31:** All the experiment codes and our preliminary model ContPRO are now released! [Click here](https://github.com/Cakeyan/ContPhy_Public/tree/main)

<table>
<tr>
    <td>
        <image src="./Teasers/fluid.gif" title="Fluid Teaser"></image>
    </td>
    <td>
        <image src="./Teasers/rope.gif" title="Rope Teaser"></image>
    </td>
</tr>
<tr>
    <td>
        <image src="./Teasers/cloth.gif" title="Cloth Teaser"></image>
    </td>
    <td>
        <image src="./Teasers/ball.gif" title="Ball Teaser"></image>
    </td>
    
</tr>
<tr>
    <td>
        <image src="./Teasers/fire.gif" title="Fire Teaser"></image>
    </td>
    <td>
        <image src="./Teasers/sensors.gif" title="Sensor Teaser"></image>
    </td>
</tr>
</table>

---
## Contents 
[Generation Tutorial](#tutorial-generating-contphy-dataset--scaling-up-customized-dataset) | [Postprocessing](#postprocessing) | [Read Data](#read-and-visualize-annotations) | [Dataset Notes](#dataset-notes) | [Manual Bug Fixes](#manual-bug-fixes) | [Citation](#citation)




## Tutorial: Generating ContPhy Dataset & Scaling-up Customized Dataset
### Using Unity3D Editor

If you are a **beginner to Unity/C# development**, familiarizing with the ContPhy dataset generation and customization logic through the user-friendly Unity Editor could quickly get you started and facilitate advanced programming. 

The steps to run generation pipeline:

#### 1. **Install Unity Editor.** 
- Download Unity3D `version 2021.3.17`. Recommend to install in `Unity Hub`.

- (Optional) *Install `DOTNET` sdk*, install VSCode C#/Unity plugins.

#### 2. **Start a new project.** 
    
- Open Unity Hub, start a `New Project`.

- Choose a HDRP (High Definition Render Pipeline) template, for example, a blank HDRP template named `High Definition 3D`. 

- Name the project, for example, `TestContPhy`.


#### 3. **Import dependencies.** 

**Note:** The complete ContPhy dataset generation depends on some **commercial packages**, whose legitimate licenses should be obtained at the ``Unity Asset Store``. Add or remove dependencies according to the requirement of your customized dataset. One can also modify the codebase to fit in **open-source alternatives**.

#### Package List
* [ContPhy-Gen](https://github.com/zzcnewly/ContPhy-Gen). 

    a. Move all the files in this repository to the directory ["TestContPhy/Assets/"](./), like this,

    ```bash
    cd path/to/the/project/TestContPhy
    git clone git@github.com:zzcnewly/ContPhy-Gen.git
    mv ./ContPhy-Gen/* ./Assets
    rm ./ContPhy-Gen
    ```
    b. Install `ffmpeg` and set its path in [PlacementConfig.cs](./Scripts/Configs/PlacementConfig.cs).

    c. Edit the path to your `bash` (Linux/Unix) or `cmd` (Windows) in [RunCommandLine.cs](./Scripts/Utils/RunCommandLine.cs)

* [Unity Perception 1.0.0-preview.1](https://docs.unity3d.com/Packages/com.unity.perception@1.0/manual/index.html)
    > Setup requires several steps.
    >
    > - Install Perception Package. In the toolbar, find `Package Manager` -> click `+` -> `from git url` -> enter `com.unity.perception` to install version `1.0.0-preview.1`
    >
    > - In the toolbar, find `Edit -> Project Settings -> Editor -> Asynchronous Shader Compilation`, uncheck it. 
    >
    > - In the toolbar, find `Edit -> Project Settings -> Perception -> Base Path`, change to `TestContPhy/output/images`. If no existing folder, create one. If no `Perception` option, try with restarting the editor. 
    >
    > - In `Assets/Settings/HDRP High Fidelity.asset` in the `Project` window, set `Lit Shader Mode` to `Both`.


* [Computational Geometry Unity Library](https://github.com/Habrador/Computational-geometry/tree/master): 

    > Required. From Github. Move `Assets/_Habrador Computational Geometry Library` to the `Assets` folder.

* [Unity Mesh Simplifier](https://github.com/Whinarn/UnityMeshSimplifier)

    > Required. From Github. Go to the `Assets` folder. Then git clone the repo.

#### Optional Packages

These packages are optional when generating a new customized dataset but **required when generating ContPhy dataset**. 

**Note:** Missing the following packages may lead to compile errors. If you do not require a specific package, comment out its associated code by "`//`".

* [Mesh Voxelizer 2.4.1](https://assetstore.unity.com/packages/tools/utilities/mesh-voxelizer-150233)

    > The tool is used to convert object meshes to 3D point clouds. Import from Unity Assets Store. 
    >
    > If you do not require generating 3D point clouds, comment line 13, 99-112 in [BaseManager.cs](./Scripts/Managers/BaseManager.cs). 
    >
    > Actually there are some **free tools** that can serve as an alternative, for example, [unity-voxel](https://github.com/mattatz/unity-voxel) at Github.

* [Filo - The Cable Simulator 1.4](https://assetstore.unity.com/packages/tools/physics/filo-the-cable-simulator-133620)
    
    > For the scenario "rope-pulley system". Import from Unity Assets Store.
    >
    > **Manual Bug Fixes:** Check [here](#manual-bug-fixes). There are some bugs in the original codebase, we need to fix them by ourselves.
    
* [Obi Fluid 6.5.1](https://assetstore.unity.com/packages/tools/physics/obi-fluid-63067) + [Obi Cloth 6.5.1](https://assetstore.unity.com/packages/tools/physics/obi-cloth-81333) + [Obi Softbody 6.5.1](https://assetstore.unity.com/packages/tools/physics/obi-softbody-130029)

    > 3 packages respectively serve for the scenario "fluid", "cloth", and "soft ball". Import from Unity Assets Store, better to follow the sequence above.

* [Ignis - Interactive Fire - URPHDRP v2.1.6](https://assetstore.unity.com/packages/tools/particles-effects/ignis-interactive-fire-urp-hdrp-181079)

    > For fire simulation. Import from Unity Assets Store. We developed a fire physical reasoning scenario based on this fire simulation engine.
    >
    > **Manual Bug Fixes**: Version 2.1.6 is not bug free. Check [Manual Bug Fixes](#manual-bug-fixes) to fix bugs.


 
 ---
#### 4. Check and run generation 

- Restart the `Unity Editor`. 
- Check there is no compile error now. If the error is from the scenario or code unrelated to your needs, comment the source code to remove the error reports.

- According to which scenario data is to be generated, modify the class name in [`MainDataGeneration.cs (line 16)`](./Scripts/MainScript/MainDataGeneration.cs).

- In editor `Project` window, open the file `Assets/Scenes/main.unity`.
    
- In editor `Game` window, choose the image quality, here we choose `FullHD(1920x1080)`.

- Click the triangular button `play` to run generation. Click the square button `stop` to stop generation.

- Check dataset in `TestContPhy/output/`. If having error generating videos, delete `TestContPhy/output/images/solo` folder and try to run again. 

- explore and modify generation parameters in the files under [`TestContPhy/Assets/Scripts/Configs`](./Scripts/Configs/), for example, 
    ```csharp
    // generate 2000 videos with complete annotations/sensor-outputs
    public const int ValidIterNum = 2000;
    // set a random seed for generation
    public const int randomSeed =5834;
    // set dataset folder name
    public const string pre_name = "fluid_slides";
    ...
    ```

- Close editor `Scene` window to slightly accelerate rendering speed. For higher performance, you may want to deploy the project to cluster server.

### Using Cluster Server

If you are an **experienced Unity/C# developer**, you can scale up the dataset on servers, e.g. high-performance computing clusters. But note that the simulation and rendering jobs in this codebase have not been optimized for GPU computing and demand more CPU resources than GPU. 

The launch command might be like this:
```bash
path/to/Unity/Hub/Editor/2021.3.17f1/Editor/Unity  -openfile "path/to/TestContPhy/Assets/Scenes/main.unity" -executeMethod EnterBatchMode.PlayScene
```
For other tips, check [Using Unity3D Editor](#using-unity3d-editor).



## Postprocessing

After generation, run postprocessing script to filter valid trials.

```bash
python path/to/TestContPhy/Assets/Scripts/Python/postprocess.py --origin path/to/original/data/folder --output path/to/processed/data/folder

# for example
cd path/to/TestContPhy
python ./Assets/Scripts/Python/postprocess.py --origin ./output/fluid_slides --output ./output/fluid_slides_new
```


## Read and Visualize Annotations

The annotations are provided in a human-readable JSON format. Additionally, a demonstration script is included to facilitate loading the dataset and visualizing its structure and dimensions.


> **Note:** Check data structure and shapes in [`Print.md`](./Teasers/Print.md).

```bash
# for example,
# only print shapes
cd path/to/TestContPhy
python ./Assets/Scripts/Python/read_and_visualize_data.py --trial_path ./output/fluid_slides_new/0 --print_shapes

# print shapes, plot the point clouds for each frame, and directly show a selected frame.
python ./Assets/Scripts/Python/read_and_visualize_data.py --trial_path ./output/fluid_slides_new/0 --print_shapes --visualize_frame --plot_save_path ./output/images/cache --selected_frame 40
```

## Dataset Notes

We are working with `.mp4` formatted videos captured at `30` frames per second, comprising `500` videos with **fixed** lengths specific to each scenario: `250` frames for fluids, `150` for ropes, `145` for cloths, and `120` for balls. However, upon decoding the videos into frames and running the data loading code to fetch the sensor data, please observe the following guidelines:

1. If there is a discrepancy of `2` frames fewer than the specified numbers for any scenario, align the $n^{th}$ frame of each video with the $(n+2)^{th}$ index of the corresponding sensor data.

2. In instances where there is a variation in the number of frames for particle/mesh sensor data among different objects within the same video—particularly noticeable when the duration of cloth frames is not synchronized with other objects—consider the $30^{th}$ frame as the start time for the cloths.

3. We have identified **a bug where rigid objects may display `incorrect poses` in the cloth scenario**. We will address and resolve this issue shortly.

Should you have any confusions or problems, please open an issue under this repo. Thanks!


## Manual Bug Fixes

```diff
# If the package "Filo" is imported, fix the following bugs.
# In the file "TestContPhy/Assets/FiloCables/Scripts/Cable.cs"

# Line 353
- float distance = link.body.SurfaceDistance(t1.Value, t2.Value, !link.orientation, false);
- link.body.AppendSamples(sampledCable, t1.Value, distance, 0, false, link.orientation);
+ //Fixed for consistancy of rope length when rolling
+ link.body.AppendSamples(sampledCable, t1.Value, link.storedCable, 0, false, link.orientation);

# Line 696
+ if (body.name != currentJoint.body1.name && body.name != currentJoint.body2.name)
// Only split if the body is a disc or a convex shape, and the raycast hit is sane.
if ((body is CableDisc || body is CableShape) && hit.distance > 0.1f && hit.distance + 0.1f < currentJoint.length)

# Line 804
// Sample the link, except if the cable is closed and this is the first link.
if (!(i == 0 && closed) || links[i].type == Link.LinkType.Attachment || links[i].type == Link.LinkType.Pinhole)
+ {
+     // Rolling links (only mid-cable)
+     if (links[i].type == Link.LinkType.Rolling)
+     {
+         Vector3? t1 = null, t2 = null;
+         if (prevJoint != null)
+             t1 = prevJoint.body2.WorldToCable(prevJoint.WorldSpaceAttachment2);
+         if (nextJoint != null)
+             t2 = nextJoint.body1.WorldToCable(nextJoint.WorldSpaceAttachment1);
+         if (t1.HasValue && t2.HasValue)
+         {
+             float distance = links[i].body.SurfaceDistance(t1.Value, t2.Value, !links[i].orientation, false);
+             if (links[i].storedCable < distance){
+                 var link0 = links[i];
+                 link0.storedCable = distance;
+                 links[i] = link0;
+             }
+         }
+     }
    SampleLink(prevJoint, links[i], nextJoint);
+ }
```

```diff
# If the package "Ignis" is imported, fix the following bug.
# In the file "TestContPhy/Assets/OAVA-Flame/Scripts/Engine/FireTrigger.cs"
Line 137
- Vector3 closestPoint = other.ClosestPointOnBounds(transform.position);
+ FireRecorder fr = flameObj.GetComponent<FireRecorder>();
+ Vector3 closestPoint = (fr != null&&fr.touchPoints.ContainsKey(other.name))? fr.touchPoints[other.name]: other.ClosestPointOnBounds(transform.position);
```



## Citation
Welcome to cite `ContPhy` if you find the paper, dataset, and implementations useful in your research :)
```bibtex
@inproceedings{zheng2024contphy,
  title={ContPhy: Continuum Physical Concept Learning and Reasoning from Videos},
  author={Zheng, Zhicheng and Yan, Xin and Chen, Zhenfang and Wang, Jingzhou and Lim, Qin Zhi Eddie and Tenenbaum, Joshua B and Gan, Chuang},
  booktitle={International Conference on Machine Learning},
  year={2024},
  organization={PMLR}
}
```
