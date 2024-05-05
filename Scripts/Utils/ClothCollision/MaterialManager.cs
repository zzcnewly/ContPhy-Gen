using ClothCollisionConfig;
using PlacementConfig;
using UnityEngine;

public class MaterialChanger : MonoBehaviour
{
    private void Start()
    {
        // Get the renderer component of the game object
        Renderer objectRenderer = GetComponent<Renderer>();

        if (objectRenderer == null)
        {
            Debug.LogError("MaterialChanger script requires a Renderer component!");
            return;
        }
        objectRenderer.material = Resources.Load<Material>(cfg.material_candidates_path + (1 + Random.Range(0, cfg.material_candidates_num)).ToString());
        objectRenderer.material.SetColor("_BASE_COLOR", SoftColor.color_rgb_dict[Random.Range(0, SoftColor.color_rgb_dict.Count)]);
    }
}