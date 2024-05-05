using System;
using System.Collections.Generic;
using UnityEngine;
using Obi;
using UnityEngine.Perception.Randomization.Randomizers;
using ClothCollisionConfig;
using PlacementConfig;
using System.Linq;
using UnityEditor;
using System.Data;

[Serializable]
[AddComponentMenu("Touchment Recorder")]
public class TouchmentRecorder : RandomizerTag {
    public ClothCollisionManager manager; // set outside
    public particleTouchmentRecorder touchmentRecorder; // set outside
    public particleTouchmentRecorder touchmentRecorder2=null; // set outside
    public List<string> touchers;
    public List<(int, string)> collisionEvents;
    public bool isStatic;
    public int MovingFrame;
    Rigidbody rb;
    

	

    private void Start() {
        isStatic = true;
        MovingFrame = -1;
        touchers = new ();
        collisionEvents = new ();
        rb = gameObject.GetComponent<Rigidbody>();
    }

    private void Update() {
        CheckVelocity();
    }

    public void CheckVelocity() {
        if(isStatic) {
            if (rb.velocity.magnitude > cfg.static_thres || rb.angularVelocity.magnitude > cfg.static_thres) {
                isStatic = false;
                if (MovingFrame == -1) {
                    MovingFrame = manager._this_frame;
                    Debug.Log("MovingFrame: " + name + ", " + MovingFrame + ", " + rb.velocity.magnitude + ", " + rb.angularVelocity.magnitude);
                }
            }
        }
    }
    
    void OnCollisionEnter(Collision collision) {
        string nm = collision.collider.name;
        if (nm!="Floor"){ // not include two floors
            touchers.Add(nm);
            collisionEvents.Add((manager._this_frame, nm));
        }
    }

    void OnCollisionExit(Collision collision) {
        string nm = collision.collider.name;
        if (nm!="Floor"){ // not include two floors
            touchers.Remove(nm);
        }
    }

    void solveClothCollision(particleTouchmentRecorder touchmentRecorder, List<(int, string)> collisionEvents, List<string> touchers){
        var cloth_col = touchmentRecorder.perObjectCollisions;
        var cloth_name = touchmentRecorder.GetComponentInChildren<ObiCloth>().name;
        bool isTouchingCloth = false;
        if (cloth_col.ContainsKey(name)) {
            var value = cloth_col[name];
            if (value.Count % 2 == 1) {
                value.Add(manager._this_frame);
            }
            if (value.Count > 0) {
                for ( int i = 0; i < value.Count; i += 2) {
                    collisionEvents.Add((value[i], cloth_name));
                }
                if (value.Last() >= manager._this_frame - 2) {
                    isTouchingCloth = true;
                }
            }
        }
        if (isTouchingCloth){
            touchers.Add(cloth_name);
        }
    }

    public Dictionary<string, object> GetAll(){
        solveClothCollision(touchmentRecorder, collisionEvents, touchers);
        if (touchmentRecorder2 != null) {
            solveClothCollision(touchmentRecorder2, collisionEvents, touchers);
        }
        Dictionary<string, object> ret = new ();
        collisionEvents.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        ret.Add("collisionEvents", collisionEvents);
        ret.Add("startMovingFrame", MovingFrame);
        ret.Add("touchers", touchers);
        return ret;
    }

    
}


[RequireComponent(typeof(ObiSolver))]
public class particleTouchmentRecorder : MonoBehaviour {
    public ClothCollisionManager manager; // set outside
    ObiSolver solver;
    public Dictionary<string, List<int>> perObjectCollisions; //starttouch-endtouch - starttouch-endtouch - ....

    private void Awake() {
        solver = GetComponent<ObiSolver>();
        perObjectCollisions = new ();
    }
 	void OnEnable () {
		solver.OnCollision += Solver_OnCollision;
	}

	void OnDisable(){
		solver.OnCollision -= Solver_OnCollision;
	}
    
    void Solver_OnCollision (object sender, Obi.ObiSolver.ObiCollisionEventArgs e)
	{
		var world = ObiColliderWorld.GetInstance();
		// just iterate over all contacts in the current frame:
		foreach (Oni.Contact contact in e.contacts) {
			// if this one is an actual collision:
            if (contact.distance < 0.01){
                ObiColliderBase col = world.colliderHandles[contact.bodyB].owner;
                if (col != null) {
                    int frame = manager._this_frame;
                    if (!perObjectCollisions.ContainsKey(col.name)) {
                        perObjectCollisions.Add(col.name, new List<int>());
                    }
                    else {
                        int count = perObjectCollisions[col.name].Count;
                        if (count > 0 && count % 2 == 0 && perObjectCollisions[col.name].Last() >= frame - 2) {
                            // refresh the time
                            perObjectCollisions[col.name][count - 1] = frame;
                            continue;
                        }
                    }
                    perObjectCollisions[col.name].Add(frame);
                }
            }
		}
	}
}