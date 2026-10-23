
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

// Level 4 - Pressurized Water Erosion
// Carves a tunnel only when the launcher accepts a valid shot.
// Stops digging after the shot limit or when the game ends.

public class WaterJetErosion : MonoBehaviour
{
    [Header("References")]
    public WaterLauncherAim aim;
    public PressureWaterLauncher launcher;
    public Tilemap dirt;
    public Tilemap obstacles;
    public Rigidbody2D waterDropPrefabRigidbody;
    public Collider2D reservoirCollider;
    public GoalReservoir goalReservoir;

    [Header("Tunnel Settings")]
    public float tunnelRadius = 0.6f;
    public float maxTrajectoryTime = 3f;

    [Header("Digging Speed")]
    [Range(0.1f, 1f)]
    public float erosionSpeed = 1f;
    public float erosionDelay = 0.02f;

    [Header("Gravity and Pressure")]
    [Tooltip("How long erosion continues after the highest point.")]
    public float downhillDigTime = 0.8f;

    [Tooltip("Minimum upward speed for full erosion.")]
    public float minUpwardSpeed = 0f;

    [Tooltip("Minimum initial launch speed needed to dig.")]
    public float minLaunchSpeed = 1f;

    [Tooltip("Minimum tunnel width during downhill erosion.")]
    [Range(0.1f, 1f)]
    public float downhillRadiusMultiplier = 0.75f;

    private Coroutine carving;

    void Start()
    {
        if (launcher == null)
        {
            Debug.LogError(
                "WaterJetErosion: Launcher not assigned!"
            );

            enabled = false;
            return;
        }

        // Only dig when a shot has been accepted.
        launcher.OnAcceptedShot += StartCarving;
    }

    void OnDisable()
    {
        if (launcher != null)
            launcher.OnAcceptedShot -= StartCarving;

        if (carving != null)
        {
            StopCoroutine(carving);
            carving = null;
        }
    }

    void StartCarving(Vector2 launchVelocity)
    {
        // Prevent carving after winning or losing.
        if (goalReservoir != null && goalReservoir.Finished)
            return;

        if (aim == null || dirt == null ||
            waterDropPrefabRigidbody == null)
        {
            Debug.LogError(
                "WaterJetErosion: Missing Aim, Dirt or WaterDrop Rigidbody."
            );
            return;
        }

        if (launchVelocity.magnitude < minLaunchSpeed)
            return;

        if (carving != null)
            StopCoroutine(carving);

        carving = StartCoroutine(
            CarveTrajectory(launchVelocity)
        );
    }

    IEnumerator CarveTrajectory(Vector2 launchVelocity)
    {
        Vector2 start = aim.anchor.position;

        Vector2 gravity =
            Physics2D.gravity *
            waterDropPrefabRigidbody.gravityScale;

        float apexTime = gravity.y < -0.001f
            ? Mathf.Max(0f, -launchVelocity.y / gravity.y)
            : maxTrajectoryTime;

        float stopDiggingTime = Mathf.Min(
            maxTrajectoryTime,
            apexTime + downhillDigTime
        );

        if (erosionDelay > 0f)
            yield return new WaitForSeconds(erosionDelay);

        float flightTime = 0f;
        Vector2 previousPosition = start;

        while (flightTime < stopDiggingTime)
        {
            // Stop active erosion when the game ends.
            if (goalReservoir != null && goalReservoir.Finished)
                break;

            float nextTime = Mathf.Min(
                flightTime +
                Time.fixedDeltaTime * erosionSpeed,
                stopDiggingTime
            );

            Vector2 nextPosition =
                start +
                launchVelocity * nextTime +
                0.5f * gravity * nextTime * nextTime;

            Vector2 segment = nextPosition - previousPosition;

            float cellSize = Mathf.Min(
                Mathf.Abs(dirt.cellSize.x),
                Mathf.Abs(dirt.cellSize.y)
            );

            float stepSize = Mathf.Max(
                0.02f,
                cellSize * 0.25f
            );

            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(segment.magnitude / stepSize)
            );

            bool blocked = false;

            for (int i = 1; i <= steps; i++)
            {
                float fraction = (float)i / steps;

                Vector2 position = Vector2.Lerp(
                    previousPosition,
                    nextPosition,
                    fraction
                );

                float currentTime = Mathf.Lerp(
                    flightTime,
                    nextTime,
                    fraction
                );

                // Stop at indestructible obstacles.
                if (obstacles != null)
                {
                    Vector3Int obstacleCell =
                        obstacles.WorldToCell(position);

                    if (obstacles.HasTile(obstacleCell))
                    {
                        blocked = true;
                        break;
                    }
                }

                // Stop at the reservoir collider.
                if (reservoirCollider != null &&
                    reservoirCollider.OverlapPoint(position))
                {
                    blocked = true;
                    break;
                }

                float radiusMultiplier = 1f;

                // Reduce erosion slightly after the apex.
                if (currentTime >= apexTime)
                {
                    float downhillProgress =
                        downhillDigTime > 0f
                        ? (currentTime - apexTime) /
                          downhillDigTime
                        : 1f;

                    radiusMultiplier = Mathf.Lerp(
                        1f,
                        downhillRadiusMultiplier,
                        Mathf.Clamp01(downhillProgress)
                    );
                }
                else if (minUpwardSpeed > 0f)
                {
                    Vector2 currentVelocity =
                        launchVelocity + gravity * currentTime;

                    if (currentVelocity.y < minUpwardSpeed)
                    {
                        radiusMultiplier = Mathf.Lerp(
                            0.75f,
                            1f,
                            Mathf.Clamp01(
                                currentVelocity.y /
                                minUpwardSpeed
                            )
                        );
                    }
                }

                float currentRadius =
                    tunnelRadius * radiusMultiplier;

                EraseTiles(position, currentRadius);
            }

            if (blocked)
                break;

            previousPosition = nextPosition;
            flightTime = nextTime;

            yield return new WaitForFixedUpdate();
        }

        carving = null;
    }

    void EraseTiles(Vector2 worldPosition, float radius)
    {
        Vector3Int center =
            dirt.WorldToCell(worldPosition);

        float cellSize = Mathf.Min(
            Mathf.Abs(dirt.cellSize.x),
            Mathf.Abs(dirt.cellSize.y)
        );

        int cellRadius = Mathf.CeilToInt(
            radius / cellSize
        ) + 1;

        float radiusSquared = radius * radius;

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                Vector3Int cell =
                    center + new Vector3Int(x, y, 0);

                Vector2 tileCenter =
                    dirt.GetCellCenterWorld(cell);

                if ((tileCenter - worldPosition).sqrMagnitude >
                    radiusSquared)
                    continue;

                if (!dirt.HasTile(cell))
                    continue;

                dirt.SetTile(cell, null);
            }
        }
    }
}
