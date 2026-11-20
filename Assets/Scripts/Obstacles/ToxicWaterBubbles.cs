
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ToxicWaterBubbles : MonoBehaviour
{
    [Header("Bubble Settings")]
    public float bubblesPerSecond = 12f;
    public float bubbleSize = 0.08f;
    public float riseSpeed = 0.4f;
    public float bubbleLifetime = 0.8f;

    [Header("Performance")]
    public int maxBubblesPerFrame = 3;

    private ParticleSystem particles;
    private readonly List<WaterContamination> contaminatedDrops =
        new List<WaterContamination>();

    private float emissionAccumulator;
    private float refreshTimer;

    void Awake()
    {
        particles = GetComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace =
            ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = false;
    }

    void Start()
    {
        particles.Play();
    }

    void Update()
    {
        refreshTimer += Time.deltaTime;

        // Refresh contaminated droplet positions periodically.
        if (refreshTimer >= 0.5f)
        {
            refreshTimer = 0f;
            RefreshContaminatedDrops();
        }

        if (contaminatedDrops.Count == 0)
            return;

        emissionAccumulator +=
            bubblesPerSecond * Time.deltaTime;

        int bubbleCount = Mathf.Min(
            Mathf.FloorToInt(emissionAccumulator),
            maxBubblesPerFrame
        );

        emissionAccumulator -= bubbleCount;

        for (int i = 0; i < bubbleCount; i++)
        {
            WaterContamination drop =
                contaminatedDrops[
                    Random.Range(0, contaminatedDrops.Count)
                ];

            if (drop == null || !drop.isContaminated)
                continue;

            ParticleSystem.EmitParams bubble =
                new ParticleSystem.EmitParams();

            bubble.position = drop.transform.position;

            bubble.velocity = new Vector3(
                Random.Range(-0.1f, 0.1f),
                riseSpeed,
                0f
            );

            bubble.startSize = bubbleSize *
                Random.Range(0.7f, 1.3f);

            bubble.startLifetime = bubbleLifetime;

            bubble.startColor =
                new Color(1f, 0.9f, 0.97f, 0.8f);

            particles.Emit(bubble, 1);
        }
    }

    void RefreshContaminatedDrops()
    {
        contaminatedDrops.Clear();

        WaterContamination[] allDrops =
            FindObjectsByType<WaterContamination>(
                FindObjectsSortMode.None
            );

        foreach (WaterContamination drop in allDrops)
        {
            if (drop != null &&
                drop.isActiveAndEnabled &&
                drop.isContaminated)
            {
                contaminatedDrops.Add(drop);
            }
        }
    }
}
