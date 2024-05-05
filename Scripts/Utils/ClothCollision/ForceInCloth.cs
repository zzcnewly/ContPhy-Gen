using UnityEngine;
using Obi;
using System.Collections.Generic;
using ClothCollisionConfig;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(ObiClothBase))]
public class ForceInCloth : MonoBehaviour
{
	ObiClothBase cloth;

    public float minForce = 0;
    public float maxForce = 10;
    public bool on = false;
    float[] forces;
    int[] counts;

    void Start()
    {
		cloth = GetComponent<ObiClothBase>();
        cloth.OnEndStep += Cloth_OnEndStep;

        forces = new float[cloth.particleCount];
        counts = new int[cloth.particleCount];
    }

    private void OnDestroy()
    {
        cloth.OnEndStep -= Cloth_OnEndStep;
    }
    Dictionary<string, float> perRegionAvgForce;
    private void Cloth_OnEndStep(ObiActor actor, float substepTime)
    {
        if ((!on) || Mathf.Approximately(maxForce, 0)) return;

        var dc = cloth.GetConstraintsByType(Oni.ConstraintType.Distance) as ObiConstraints<ObiDistanceConstraintsBatch>;
        var sc = cloth.solver.GetConstraintsByType(Oni.ConstraintType.Distance) as ObiConstraints<ObiDistanceConstraintsBatch>;

        if (dc != null && sc != null)
        {
            Debug.Log("Cloth_OnEndStep");
            float sqrTime = substepTime * substepTime;

            for (int i = 0; i < cloth.solverIndices.Length; ++i) {
                forces[i] = 0;
                counts[i] = 0;
            }

            for (int j = 0; j < dc.batches.Count; ++j)
            {
                var batch = dc.batches[j] as ObiDistanceConstraintsBatch;
                var solverBatch = sc.batches[j] as ObiDistanceConstraintsBatch;

                for (int i = 0; i < batch.activeConstraintCount; i++)
                {
                    // divide lambda by squared delta time to get force in newtons:
                    int offset = cloth.solverBatchOffsets[(int)Oni.ConstraintType.Distance][j];
                    float force = -solverBatch.lambdas[offset + i] / sqrTime;

                    int p1 = batch.particleIndices[i * 2];
                    int p2 = batch.particleIndices[i * 2+1];

                    if (cloth.solver.invMasses[cloth.solverIndices[p1]] > 0 ||
                        cloth.solver.invMasses[cloth.solverIndices[p2]] > 0)
                    {
                        forces[p1] += force;
                        forces[p2] += force;

                        counts[p1]++;
                        counts[p2]++;
                    }
                }
            }
            for (int i = 0; i < cloth.solverIndices.Length; ++i) {
                forces[i] = forces[i] / counts[i];
            }
            
            // visualize forces magnitude as color:
            if (cfg.debug_force) {
                // average force over each particle, map to color, and reset forces:
                int total = cloth.solverIndices.Length;
                bool move = true;
                float max = 0;
                for (int i = 0; i < total; ++i)
                {
                    if (counts[i] > 0)
                    {
                        if (forces[i] > max) max = forces[i];
                    }
                }
                if (temp == null) {move=false;temp = new List<GameObject>();}
                for (int i = 0; i < total; ++i)
                {
                    if (counts[i] > 0)
                    {
                        int solverIndex = cloth.solverIndices[i];
                        float color = forces[i] / max;
                        if(move){
                            temp[i].transform.localPosition = cloth.solver.renderablePositions[solverIndex];
                            temp[i].GetComponent<Renderer>().material.color = new Color(color, color, color, 1);
                        }
                        else {
                            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            go.GetComponent<Collider>().enabled = false;
                            go.transform.parent = cloth.solver.transform;
                            go.transform.localPosition = cloth.solver.renderablePositions[solverIndex];
                            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                            go.GetComponent<Renderer>().material.color = new Color(color, color, color, 1);
                            temp.Add(go);
                        }
                    }
                }
            }
        }
    }
    List<GameObject> temp = null;
    public Dictionary<string, object> GetAll(){
        // 4 qradrants  
        // A red    corner1 quadrant1 zuoxia 
        // B blue   corner2 quadrant2 youxia | E purple center
        // C yellow corner3 quadrant3 youshang
        // D green  corner4 quadrant4 zuoshang 
        List<string> quadrantNames = new List<string>(){ "Red", "Blue", "Yellow", "Green" };
        perRegionAvgForce = new();
        for (int k = 1 ; k < 5 ; k++){
            float totalForce = 0;
            string quadrantName = quadrantNames[k-1];
            var ids = cloth.blueprint.groups.Find(x => x.name == "Quadrant" + k.ToString()).particleIndices;
            for (int i = 0; i < ids.Count; i++){
                totalForce += forces[ids[i]];
            }
            float avgForce = totalForce / ids.Count;
            perRegionAvgForce.Add(quadrantName, avgForce);
        }
        Dictionary<string, object> ret = new ();
        ret.Add("perRegionAvgForce", perRegionAvgForce);
        return ret;
    }
}
