
using UnityEngine;

[RequireComponent(typeof(WaterDropVisual))]
public class WaterContamination : MonoBehaviour
{
    [Header("Contamination Settings")]
    public bool isContaminated = false;

    public Color contaminatedColor =
        new Color(1f, 0.75f, 0.85f, 1f);

    private WaterDropVisual waterVisual;

    void Awake()
    {
        waterVisual = GetComponent<WaterDropVisual>();

        if (waterVisual == null)
        {
            Debug.LogError(
                "WaterContamination: Missing WaterDropVisual on "
                + gameObject.name,
                gameObject
            );
        }
    }

    void Start()
    {
        // Pink water starts contaminated.
        if (isContaminated)
        {
            Contaminate();
        }
    }

    public void Contaminate()
    {
        if (isContaminated &&
            waterVisual != null &&
            waterVisual.waterColor == contaminatedColor)
        {
            return;
        }

        isContaminated = true;

        if (waterVisual != null)
        {
            waterVisual.waterColor = contaminatedColor;
        }

        Debug.Log(
            "Water contaminated: " + gameObject.name,
            gameObject
        );
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        WaterContamination other =
            collision.gameObject.GetComponent<WaterContamination>();

        if (other == null)
            return;

        if (isContaminated && !other.isContaminated)
        {
            Debug.Log("Pink water contaminated blue water!");
            other.Contaminate();
        }
        else if (!isContaminated && other.isContaminated)
        {
            Debug.Log("Blue water touched pink water!");
            Contaminate();
        }
    }
}
