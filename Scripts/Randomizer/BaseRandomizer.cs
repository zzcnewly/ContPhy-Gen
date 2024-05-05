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
using UnityEngine.PlayerLoop;
using System.Runtime.InteropServices;
using UnityEditorInternal;
using UnityEngine.WSA;
using UnityEngine.Diagnostics;
using UnityEditor.Search;
using UnityEngine.Perception.GroundTruth.LabelManagement;
using UnityEditor.VersionControl;
using UnityEngine.UIElements;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.Perception.GroundTruth.Labelers;
using UnityEngine.Perception.GroundTruth;

namespace UnityEngine.Perception.Randomization.Randomizers{
    public class BaseRandomizer : Randomizer
    {
        public int _this_frame = 0;
        protected BaseRandomizer(){}
        public virtual Dictionary<string, object> GetAll(){return new Dictionary<string, object>();}
        public virtual void SaveAll(string path_without_slash){}

        protected override void OnIterationStart(){
            _this_frame = 0;
        }
        protected override void OnUpdate(){
            _this_frame += 1;
        }
    }
}