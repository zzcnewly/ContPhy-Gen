
using UnityEngine.Perception.Randomization.Randomizers;


namespace UnityEngine.Perception.Randomization.Scenarios
{
   
// Main Script Added to Camera 
[AddComponentMenu("Perception/Scenarios/Main Scripted Scenario")]
public class MainDataGeneration : UnityEngine.Perception.Randomization.Scenarios.FixedLengthScenario
{

    // ###################################################################
    // ## Change the manager class to your self-defined manager class.  ##
    // ###################################################################
    public BaseManager manager = new FluidSlidesManager(); // "Fluid" Scenario
    // public BaseManager manager = new PulleyGroupManager(); // "Rope-Pully" Scenario
    // public BaseManager manager = new ClothCollisionManager(); // "Cloth" Scenario
    // public BaseManager manager = new SoftBodyManager(); // "Soft Ball" Scenario
    // public BaseManager manager = new FireSpreadManager(); // Additional "Fire" Scenario
    // TODO: Check some ContPhy manager classes here
    

    protected override void OnAwake() 
    {
        // // Setting Params (Optional)
        // framesPerIteration = 1000;
        // constants.randomSeed = 1200;//811045;
        // constants.iterationCount = 20;
        // constants.startIteration = 0;
        // RectTransform rt = UICanvas.GetComponent (typeof (RectTransform)) as RectTransform;
        // rt.sizeDelta = new Vector2 (100, 100);
        // Screen.SetResolution(224, 224, false);
        // Build the Schemes!
        manager.BuildScheme();
        // Start Simulation!
        base.OnAwake();
    }

    
}
}
