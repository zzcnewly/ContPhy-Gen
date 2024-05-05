
using System.Collections.Generic;

namespace PlacementConfig{

class GlobalSettings{
    public const int particleCaptureRate = 2;
}

class SystemSettings{
    public const string ffmpeg_path = "C:/Users/87242/HDRP_Pipeline_after_webMaking/ffmpeg-master-latest-win64-gpl/bin/ffmpeg";
    // TODO: public const string ffmpeg_path = "path/to/your/bin/ffmpeg";
}

class GlobalShapeGrab{
    public const int frequency = 30;
}

class SequenceNumberName {
    public static Dictionary<int, string> Num2Seq = new Dictionary<int, string>(){
        {1,   "1st"},   
        {2,   "2nd"},        
        {3,   "3rd"},   
        {4,   "4th"},  
        {5,   "5th"},  
        {6,   "6th"},  
        {7,   "7th"}, 
        {8,   "8th"},   
        {9,   "9th"}
    };
}

class PlacementType {
    public const int Cube            = 1;
    public const int Sphere          = 2; 
    public const int Pyramid         = 3; 
    public const int Cone            = 4;
    public const int ChamferCube     = 5; 
    public const int ChanferCylinder = 6; 
    public const int Torus           = 7; 
    public const int Prism           = 8; 
    public const int Capsule         = 9;
    public const int Cylinder        = 10;
    public const int Cubiod          = 11;
    public const int Collider        = -1;
    public const int Plate           = 12;
    public const int Rod             = 13;
    public static Dictionary<int, string> shape_name_dict = new Dictionary<int, string>(){
            {1,  "Cube"},
            {2,  "Sphere"},
            {11, "Pillar"},
            {12, "Plate"},
            {13, "Rod"},
    };
};




class PlacementPrefabs{
    public static Dictionary<int, string> type_prefab_dict = new Dictionary<int, string>(){
        {PlacementType.Cube,            "Prefabs/Cloth Collision Blocks/Cloth/Box"},
        {PlacementType.Sphere,          "Prefabs/Cloth Collision Blocks/Cloth/Sphere"},
        {PlacementType.Pyramid,         "Prefabs/Cloth Collision Blocks/Cloth/Pyramid"},
        {PlacementType.Cone,            "Prefabs/Cloth Collision Blocks/Cloth/Cone"},
        {PlacementType.ChamferCube,     "Prefabs/Cloth Collision Blocks/Cloth/ChamferBox"},
        {PlacementType.ChanferCylinder, "Prefabs/Cloth Collision Blocks/Cloth/ChanferCylinder"},
        {PlacementType.Torus,           "Prefabs/Cloth Collision Blocks/Cloth/Torus"},
        {PlacementType.Prism,           "Prefabs/Cloth Collision Blocks/Cloth/Prism"},
        {PlacementType.Capsule,         "Prefabs/Cloth Collision Blocks/Cloth/Capsule"},
        {PlacementType.Cylinder,        "Prefabs/Cloth Collision Blocks/Cloth/Cylinder"},
        {PlacementType.Collider,        "Prefabs/Cloth Collision Blocks/Cloth/Collider"},
    };
}
class SoftColor{
    public const int Red     = 0;
    public const int Orange  = 1;
    public const int Yellow  = 2;
    public const int Green   = 3;
    public const int Cyan    = 4;
    public const int Blue    = 5;
    public const int Purple  = 6;
    public const int Pink    = 7;
    public const int Brown   = 8;
    public const int Gray    = 9;
    public const int Black   = 10;
    public const int White   = 11;
    public const int LightBlue  = 12;
    public const int LightGreen  = 13;
    public const int LightYellow  = 14;
    public const int LightCoral  = 15;
    public const int LightPurple  = 16;

    public static Dictionary<int, UnityEngine.Color> color_rgb_dict = new Dictionary<int, UnityEngine.Color>(){
        {0, new UnityEngine.Color(201f/255f, 0f/255f, 22f/255f, 1.0f)}, //deepred
        {1, new UnityEngine.Color(229f/255f, 131f/255f, 8f/255f, 1)}, // orange
        {2, new UnityEngine.Color(244f/255f, 208f/255f, 0, 1f)}, //yellow
        {3, new UnityEngine.Color(101f/255f, 147f/255f, 74f/255f, 1)}, // green 
        {4, new UnityEngine.Color(35f/255f, 235f/255f, 185f/255f, 1)}, // cyan
        {5, new UnityEngine.Color(56f/255f, 118f/255f, 248f/255f, 1)}, // blue
        {6, new UnityEngine.Color(154f/255f, 78f/255f, 174f/255f, 1)}, // purple
        {7, new UnityEngine.Color(255f/255f, 137f/255f, 153f/255f, 1)}, // pink
        {8, new UnityEngine.Color(118f/255f, 77f/255f, 57f/255f, 1)}, // brown
        {9, new UnityEngine.Color(128f/255f, 128f/255f, 128f/255f, 1)}, //gray
        {10, new UnityEngine.Color(0f, 0f, 0f, 1)}, // black
        {11, new UnityEngine.Color(245f/255f, 245f/255f, 245f/255f, 1)}, // White
        {12, new UnityEngine.Color(202f/255f, 235f/255f, 216f/255f, 1)}, // LightBlue
        {13, new UnityEngine.Color(240f/255f, 255f/255f, 240f/255f, 1)}, // LightGreen
        {14, new UnityEngine.Color(255f/255f, 255f/255f, 224f/255f, 1)}, // LightYellow
        {15, new UnityEngine.Color(240f/255f, 128f/255f, 128f/255f, 1)}, // LightCoral
        {16, new UnityEngine.Color(221f/255f, 160f/255f, 221f/255f, 1)}, // LightPurple
    };
    public static Dictionary<int, string> color_name_dict 
        = new Dictionary<int, string>(){
            {0, "Red"},
            {1, "Orange"},
            {2, "Yellow"},
            {3, "Green"},
            {4, "Cyan"},
            {5, "Blue"},
            {6, "Purple"},
            {7, "Pink"},//
            {8, "Brown"},
            {9, "Gray"},
            {10, "Black"},
            {11, "White"},
            {12, "Light Blue"},
            {13, "Light Green"},
            {14, "Light Yellow"},
            {15, "Light Coral"},
            {16, "Light Purple"},
    };
};
}