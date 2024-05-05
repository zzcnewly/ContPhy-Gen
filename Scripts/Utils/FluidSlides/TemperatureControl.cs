using UnityEngine;
using Obi;
using System.Collections.Generic;
using FluidSlidesConfig;
using UnityEditor.VersionControl;
using UnityEditor.Rendering;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.UI;
using System.ComponentModel.Design;
using UnityEngine.UIElements.Experimental;
using System;
using System.Linq;
using Unity.Collections;
using Unity.VisualScripting;
using Ignis;
// using System.Numerics;

[RequireComponent(typeof(ObiCollider))]
public class TemperatureIndicator : MonoBehaviour {
    public string Temperature;
}


[RequireComponent(typeof(ObiSolver))]
public class HeatTransport : MonoBehaviour {
    public Dictionary<string, string> Obj2Temp = new Dictionary<string, string>(); // should assign in beginning // NOTE: cannot cover all the sticks in the scene
	public Dictionary<string, (Vector3, Vector3)> receptorCenterAndScale;
	public Dictionary<string, (double, double, double, double)> receptorGuidedMinXMaxXMinYMaxY;
	public Dictionary<string, float> fluidDensity;
 	private ObiSolver solver;
	public float boiling_limit = cfg.vis_boiling_limit;
	public float boiling_sft = cfg.boiling_sft;
	public float boiling_buoy = cfg.boiling_buoy;
	public List<string>[] perParticlePath;
	public List<int>[] perParticleStateB, perParticleStateA;
	public List<bool>[] perParticleValid;
	public bool[] perParticleCachePosition;

	public Dictionary<string, Dictionary<string, int>> perReceptorFluidStat;
	public List<(string, string)> allDensityRelations;
	public Dictionary<string, Dictionary<string, int>> perEmitterStickStat = new();
	public Dictionary<string, Dictionary<string, int>> perEmitterStickStat_refined = new();
	public Dictionary<string, Dictionary<string, int>> perStickBoilingLiquilizeStickStat = new();
	public Dictionary<string, Dictionary<int, Dictionary<string, int>>> perStickHappenedStateofMatterChangeFluidTypeStat = new();
	public Dictionary<string, string> temperatureAskableStick = new();
	public List<string> toRemoveSticks = new();
	public int[] NowDataState;

	void Awake(){
		solver = GetComponent<Obi.ObiSolver>();
	}
	void Start(){
        if (Obj2Temp.Count == 0) throw new System.Exception("Obj2Temp is Unassigned!");
	}
	void OnEnable () {
		solver.OnCollision += Solver_OnCollision;
	}

	void OnDisable(){
		solver.OnCollision -= Solver_OnCollision;
	}

	void initSetDownParticles(int num){
		int actorNum = solver.actors.Count;
		for (int k = 0; k < actorNum; k++){
			var actor = solver.actors[k] as ObiEmitter;
			int count = actor.solverIndices.Length;
			for (int i = 0; i < count; i++) {
				if (i % cfg.interval_particle_sample_output != 0) continue;
				int index = actor.solverIndices[i];
				perParticleCachePosition[index] = true;
			}
		}
	}
	int gas_filter, non_filter;
	void initCache(){
		if (perParticlePath == null){ 
			int num = solver.positions.count;
			perParticlePath = new List<string>[num];
			perParticleStateA = new List<int>[num];
			perParticleStateB = new List<int>[num];
			perParticleValid = new List<bool>[num];
			perParticleCachePosition = new bool[num];
			NowDataState = new int[num];
			for (int i = 0; i < num; ++i){
				perParticlePath[i] = new List<string>();
				perParticleStateA[i] = new List<int>();
				perParticleStateB[i] = new List<int>();
				perParticleValid[i] = new List<bool>();
				perParticleCachePosition[i] = false;
				NowDataState[i] = cfg.LIQUID_;
			}
			initSetDownParticles(num);
			gas_filter = ObiUtils.MakeFilter(1 << 0, 2);
			non_filter = ObiUtils.MakeFilter(ObiUtils.CollideWithNothing, 3);
		}
	}

	void deactivateParticle(int k){
		solver.phases[k] &= (int)(~ObiUtils.ParticleFlags.Fluid);
		solver.invMasses[k] = 0;
	}

	void Solver_OnCollision (object sender, Obi.ObiSolver.ObiCollisionEventArgs e)
	{
		initCache();
		int case0 = UnityEngine.Random.Range(0, 10000);
		var colliderWorld = ObiColliderWorld.GetInstance();
        int filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);
		for (int i = 0;  i < e.contacts.Count; ++i)
		{
			var contact = e.contacts.Data[i];
			if (contact.distance < 0.001f)
			{
				var col = colliderWorld.colliderHandles[contact.bodyB].owner;
				if (col != null)
				{
					int simplexStart = solver.simplexCounts.GetSimplexStartAndSize(contact.bodyA, out int simplexSize);
					for (int i0 = 0; i0 < simplexSize; ++i0)
					{
						int k = solver.simplices[simplexStart + i0];
						int preState = NowDataState[k];
						if (preState == cfg.GAS_DEAD_) continue;
						string emittername = solver.particleToActor[k].actor.name;
						int state = cfg.LIQUID_;
						string colName = col.name;
						string curTemp = cfg.NORMAL;
						if (Obj2Temp.ContainsKey(colName)){
							curTemp = Obj2Temp[colName];
						}
						if (curTemp == cfg.BOILING){
							solver.viscosities[k] = boiling_limit;
							solver.buoyancies[k] = boiling_buoy;
							solver.surfaceTension[k] = boiling_sft;
							solver.filters[k] = gas_filter;
							NowDataState[k] = cfg.GAS_;
							state = cfg.GAS_;
						}
						else {// normal
							if (preState == cfg.GAS_){ // "delete"
								deactivateParticle(k);
								solver.filters[k] = non_filter;
								NowDataState[k] = cfg.GAS_DEAD_;
								state = cfg.GAS_DEAD_;
							}
							else if (preState == cfg.LIQUID_) {
								NowDataState[k] = cfg.LIQUID_;
								state = cfg.LIQUID_;
							}
							else{
								throw new System.Exception("Unknown State generated: " + preState.ToString());
							}
						}
						int count = perParticlePath[k].Count;
						if (count > 0 && perParticlePath[k][count - 1] == colName){}//pass
						else {
							perParticlePath[k].Add(colName);
							perParticleStateB[k].Add(preState);
							perParticleStateA[k].Add(state);
							perParticleValid[k].Add(((count > 0 && !perParticleValid[k][count - 1]) || state == cfg.GAS_) ? false : true);
						}
					}
				}
			}
		}
	}


	public Dictionary<string, object> Summary(){
		// when all the process finishes, just output the diverse annotation/explanation of the path
		// 1. each emitters passing-stick's particle total number,
		// 2. each receptor's fluid statistics
		perReceptorFluidStat = new();
		perEmitterStickStat = new();
		perStickBoilingLiquilizeStickStat = new();
		perStickHappenedStateofMatterChangeFluidTypeStat = new();
		for (int k=0; k < perParticlePath.Length; ++k){ // per particle
			// receptor statistic
			var pos = solver.positions[k];// actually local pos == absolute pos
			var emitter = solver.particleToActor[k].actor.name;
			foreach (var receptor in receptorCenterAndScale.Keys){
				var center = receptorCenterAndScale[receptor].Item1;
				var scale = receptorCenterAndScale[receptor].Item2;
				(double, double, double, double) guidedxxyy = receptorGuidedMinXMaxXMinYMaxY[receptor];
				bool inLowC = pos.x > center.x - scale.x / 2 && pos.x < center.x + scale.x / 2 &&
								pos.y > center.y - scale.y / 2 && pos.y < center.y + scale.y / 2;
				bool inHighC = pos.x > guidedxxyy.Item1 && pos.x < guidedxxyy.Item2 &&
								pos.y > guidedxxyy.Item3 && pos.y < guidedxxyy.Item4;
				if (inLowC || inHighC){
					if (!perReceptorFluidStat.ContainsKey(receptor)){
						perReceptorFluidStat.Add(receptor, new ());
					}
					var FluidStat = perReceptorFluidStat[receptor];
					if (!FluidStat.ContainsKey(emitter)){
						FluidStat.Add(emitter, 0);
					}
					FluidStat[emitter] += 1;
				}
			}
			if(!perEmitterStickStat.ContainsKey(emitter)) {
				perEmitterStickStat.Add(emitter, new());
			}
			var stick_dict = perEmitterStickStat[emitter];
			var valid = perParticleValid[k];
			int leastUnVaporizedTime = 1 + valid.FindIndex(x => x == false);
			if (leastUnVaporizedTime == 0) leastUnVaporizedTime = valid.Count; 
			// get the valid data sequence
			var path = perParticlePath[k].GetRange(0, leastUnVaporizedTime);
			// get unique stick collider data
			var path_uniq = new HashSet<string>(path); 
			foreach (string stick in path_uniq){
				if (!stick_dict.ContainsKey(stick)){
					stick_dict.Add(stick, 0);
				}
				stick_dict[stick] += 1;
			}
			// deal with boiling destination issue
			if (leastUnVaporizedTime < valid.Count)  { // with boiling
				string boilStick = perParticlePath[k][leastUnVaporizedTime - 1]; // check its boiling in output
				if (!perStickBoilingLiquilizeStickStat.ContainsKey(boilStick)) {
					perStickBoilingLiquilizeStickStat.Add(boilStick, new Dictionary<string, int>());
				}
				var dict1 = perStickBoilingLiquilizeStickStat[boilStick];
				for (int i=leastUnVaporizedTime; i<perParticlePath[k].Count; i++){
					if (perParticleStateA[k][i] != cfg.GAS_){
						var boilStick_ = perParticlePath[k][i];
						if (!dict1.ContainsKey(boilStick_)) dict1.Add(boilStick_, 0);
						dict1[boilStick_] += 1;
						break;
					}
				}
			}
			// deal with state of matter transformation
			// deal with viscosity change per stick per fluid
			string fluidName = emitter;
			List<string> temp_happened_derepeat = new();
			for (int i=0; i < perParticleStateA[k].Count; i++){
				var stateB = perParticleStateB[k][i];
				var stateA = perParticleStateA[k][i];
				if (stateB != stateA){ // happened a state of matter change event
					int stateChangeType;
					if (stateB == cfg.LIQUID_){
						stateChangeType = cfg.LIQUID_GAS;
					}
					else {
						stateChangeType = cfg.GAS_LIQUID;
					}
					string stickName = perParticlePath[k][i];
					if(isRegularStick(stickName)) {
						if (!perStickHappenedStateofMatterChangeFluidTypeStat.ContainsKey(stickName)){
							perStickHappenedStateofMatterChangeFluidTypeStat.Add(stickName, new Dictionary<int, Dictionary<string, int>>());
						}
						var StateofMatterChangeFluidTypeStat = perStickHappenedStateofMatterChangeFluidTypeStat[stickName];
						if (!StateofMatterChangeFluidTypeStat.ContainsKey(stateChangeType)){
							StateofMatterChangeFluidTypeStat.Add(stateChangeType, new Dictionary<string, int>());
						}
						if (!temp_happened_derepeat.Contains(stickName + "|" + stateChangeType))
						{
							var FluidTypeStat = StateofMatterChangeFluidTypeStat[stateChangeType];
							if (!FluidTypeStat.ContainsKey(fluidName)){
								FluidTypeStat.Add(fluidName, 0);
							}
							FluidTypeStat[fluidName] += 1;
							temp_happened_derepeat.Add(stickName + "|" + stateChangeType);
						}
					}
				}
			}
		}
		// 1.1 refine the collision particle number,  omit if too little < collide_particle_num_to_be_valid
		perEmitterStickStat_refined = new();
		temperatureAskableStick = new();
		foreach (var emitter in perEmitterStickStat.Keys){
			perEmitterStickStat_refined.Add(emitter, new Dictionary<string, int>());
			var stick_dict = perEmitterStickStat_refined[emitter];
			foreach (var stick in perEmitterStickStat[emitter].Keys){
				var colNum = perEmitterStickStat[emitter][stick];
				if (stick.Contains("Emitter")) {
					// the emitter inner touches, delete
					string key_ = emitter + " Emitter";
					if (!stick_dict.ContainsKey(key_)){
						stick_dict.Add(key_, colNum);
					}
					else {
						stick_dict[key_] = stick_dict[key_] > colNum? stick_dict[key_] : colNum;
					}
				}
				else {
					if (colNum > cfg.collide_particle_num_to_be_valid){
						stick_dict.Add(stick, colNum);
						if (isRegularStick(stick)){
							if (!temperatureAskableStick.ContainsKey(stick)) {
								temperatureAskableStick.Add(stick, Obj2Temp.ContainsKey(stick) ? Obj2Temp[stick] : cfg.NORMAL);
							}
						}
					}
				}
			}
		}
		// refine the receptor fluid statistics
		Dictionary<string, Dictionary<string, int>> perReceptorFluidStat_refined = new();
		foreach (var receptor in perReceptorFluidStat.Keys){
			var FluidStat = perReceptorFluidStat[receptor];
			var FluidStat_refined = new Dictionary<string, int>();
			foreach (var emitter in FluidStat.Keys){
				if (FluidStat[emitter] > cfg.receptor_particle_num_to_be_valid){
					FluidStat_refined.Add(emitter, FluidStat[emitter]);
				}
			}
			perReceptorFluidStat_refined.Add(receptor, FluidStat_refined);
		}
		// get the deduced density relations from perReceptorFluidStat
		var relations = new List<Relation>();
		foreach (var receptor in perReceptorFluidStat_refined.Keys){
			var FluidStat = perReceptorFluidStat_refined[receptor];
			foreach (var emitter in FluidStat.Keys){
				foreach (var emitter_ in FluidStat.Keys){
					if (emitter != emitter_){
						if (fluidDensity[emitter] > fluidDensity[emitter_])
							relations.Add(new Relation(emitter, emitter_));
						else{
							relations.Add(new Relation(emitter_, emitter));
						}
					}
				}
			}
		}
		var allRelations = DeduceAllRelations(relations);
		allDensityRelations = new ();
		foreach (var relation in allRelations){
			allDensityRelations.Add((relation.Greater, relation.Smaller));
		}

		// make a CF removal list from perEmitterStickStat
		toRemoveSticks = new();
		foreach(string fluid in perEmitterStickStat.Keys){
			int max_particle = -1;
			string max_stick = "";
			int max_particle2 = -1;
			string max_stick2 = "";
			foreach(string stick in perEmitterStickStat[fluid].Keys){
				int num = perEmitterStickStat[fluid][stick];
				if ( isRegularStick(stick)){
					if (num > max_particle){
						max_particle2 = max_particle;
						max_stick2 = max_stick;
						max_particle = num;
						max_stick = stick;
					}
					else if (num > max_particle2){
						max_particle2 = num;
						max_stick2 = stick;
					}
				}
			}
			if (max_particle2 > cfg.min_pass_particles && !toRemoveSticks.Contains(max_stick2)){
				toRemoveSticks.Add(max_stick2);
			}
			if (max_particle > cfg.min_pass_particles && !toRemoveSticks.Contains(max_stick)){
				toRemoveSticks.Add(max_stick);
			}
		}
		if (toRemoveSticks.Count < 1){ // too less to remove
			toRemoveSticks.Add(Obj2Temp.Keys.ToList()[0]);
		}
		if (toRemoveSticks.Count > cfg.max_cf_remove){ // too much to remove
			for (int i = 0; i < toRemoveSticks.Count; i++) { // shuffle the list
				string temp = toRemoveSticks[i];
				int randomIndex = UnityEngine.Random.Range(i, toRemoveSticks.Count);
				toRemoveSticks[i] = toRemoveSticks[randomIndex];
				toRemoveSticks[randomIndex] = temp;
			}
			toRemoveSticks = toRemoveSticks.GetRange(0, cfg.max_cf_remove);
		}

		// output the results
		var ret = new Dictionary<string, object>();
		ret.Add("perEmitterStickStat", perEmitterStickStat_refined);
		ret.Add("temperatureStick", Obj2Temp);
		ret.Add("temperatureAskableStick", temperatureAskableStick);
		ret.Add("perReceptorFluidStat", perReceptorFluidStat_refined);
		ret.Add("allDensityRelations", allDensityRelations);

		return ret;
	}
	
	public class Relation {
        public string Greater { get; set; }
        public string Smaller { get; set; }

        public Relation(string greater, string smaller)
        {
            Greater = greater;
            Smaller = smaller;
        }
    }

    List<Relation> DeduceAllRelations(List<Relation> initialRelations) {
        var allRelations = new List<Relation>(initialRelations);
		// unique the relations
		var temp = new HashSet<(string, string)>();
		foreach (var relation in allRelations){
			temp.Add((relation.Greater, relation.Smaller));
		}
		allRelations = new List<Relation>();
		foreach (var relation in temp){
			allRelations.Add(new Relation(relation.Item1, relation.Item2));
		}
        bool newRelationFound;

        do
        {
            newRelationFound = false;

            for (int i = 0; i < allRelations.Count; i++)
            {
                for (int j = 0; j < allRelations.Count; j++)
                {
                    if (allRelations[i].Smaller == allRelations[j].Greater &&
                        !RelationExists(allRelations, allRelations[i].Greater, allRelations[j].Smaller))
                    {
                        allRelations.Add(new Relation(allRelations[i].Greater, allRelations[j].Smaller));
                        newRelationFound = true;
                    }
                }
            }

        } while (newRelationFound);

        return allRelations;
    }

	static bool RelationExists(List<Relation> relations, string greater, string smaller) {
        foreach (var relation in relations)
        {
            if (relation.Greater == greater && relation.Smaller == smaller)
            {
                return true;
            }
        }

        return false;
    }
	bool isRegularStick(string name){
		if (name.Contains("Stick") && !name.Contains("Guided")) return true;
		else return false;
	}
}
