using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class YZCH_TerrainEditor : MonoBehaviour
{
    public enum Mode
    {
        NONE,
        EDIT_TERRAIN,
        PAINT_TERRAIN,
        PAINT_DETAILS,
    }

    public enum TerrainEditMode
    {
        RAISE_LOWER,
        SMOOTH,
        FLATTEN_BY_HEIGHT,
        FLATTEN_BY_VALUE,
    }

    public EditorManager editorManager;

    public TerrainImage brushImage;
    public LineRenderer brushImageLR;
    public float buildTargetScaleMultiplier = 1f;

    private Terrain t;
    private TerrainData tData;
    [HideInInspector] public Mode mode;
    [HideInInspector] public TerrainEditMode editTerrainMode;

    private float[,] influencingPoints;
    public float strength = 1;
    private float area = 1;
    public float height = 1;
    private float lastHeight = 1;
    private float strengthSave;

    [Header("UI")]
    public Text brushSizeTxt;
    public Text pointingHeightTxt;
    public Slider brushHeight;

    private int selectedTextureIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        t = FindObjectOfType<Terrain>();
        tData = t.terrainData;
    }

    // Update is called once per frame
    void Update()
    {
        if (mode != Mode.NONE && !BarBuildings.IsPointerOverUIElement())
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 300, 1 << 13))
            {

                if (editTerrainMode == TerrainEditMode.FLATTEN_BY_HEIGHT)
                {
                    brushImage.transform.position = new Vector3(hit.point.x, hit.point.y, hit.point.z);
                    brushImage.mode = TerrainImage.Mode.FLAT;

                    brushImageLR.enabled = true;
                    brushImageLR.SetPosition(0, hit.point);
                    brushImageLR.SetPosition(1, brushImage.transform.position);
                } else if (editTerrainMode == TerrainEditMode.FLATTEN_BY_VALUE)
                {
                    float height = brushHeight.value * tData.size.y;
                    brushImage.transform.position = new Vector3(hit.point.x, height, hit.point.z);
                    brushImage.mode = TerrainImage.Mode.NONE;

                    brushImageLR.enabled = true;
                    brushImageLR.SetPosition(0, hit.point);
                    brushImageLR.SetPosition(1, brushImage.transform.position);
                }
                else
                {
                    brushImage.transform.position = new Vector3(hit.point.x, hit.point.y, hit.point.z);
                    brushImage.mode = TerrainImage.Mode.TERRAIN;
                    brushImageLR.enabled = false;
                }

                //Get height data
                lastHeight = hit.point.y / tData.size.y;
                pointingHeightTxt.text = string.Format("Pointing height : {0:0.00}", lastHeight);

                if (Input.GetMouseButtonDown(0))
                {
                    //UNDO
                    if (mode == Mode.EDIT_TERRAIN)
                    {
                        float[,] heights = tData.GetHeights(0, 0, tData.heightmapResolution, tData.heightmapResolution);
                        byte[] data = ArrayWorker.Float2DToByte1D(heights);
                        editorManager.createUndoAction(EditorManager.UndoType.TERRAIN_EDIT, data, 0);
                    } else if (mode == Mode.PAINT_TERRAIN)
                    {
                        float[,,] textures = tData.GetAlphamaps(0, 0, tData.alphamapResolution, tData.alphamapResolution);
                        byte[] data = ArrayWorker.Float3DToByte1D(textures);
                        editorManager.createUndoAction(EditorManager.UndoType.TERRAIN_PAINT, data, 0);
                    }
                }

                //Changing terrain
                if (Input.GetMouseButton(0))
                {
                    //Copy terrain height
                    if (Input.GetKey(KeyCode.C))
                    {
                        float h = t.SampleHeight(hit.point) / tData.size.y;
                        brushHeight.value = h;
                        return;
                    }

                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        strengthSave = -strength;
                    }
                    else
                    {
                        strengthSave = strength;
                    }

                    switch (mode)
                    {
                        case Mode.EDIT_TERRAIN:
                            Vector3Int hitPoint = new Vector3Int((int)hit.point.x, 0, (int)hit.point.z);
                            int startX = (int)((hitPoint.x - area) / tData.size.x * tData.heightmapResolution);
                            int startY = (int)((hitPoint.z - area) / tData.size.z * tData.heightmapResolution);
                            int endX = (int)((hitPoint.x + area) / tData.size.x * tData.heightmapResolution);
                            int endY = (int)((hitPoint.z + area) / tData.size.z * tData.heightmapResolution);

                            float expectedWidth = endX - startX;
                            float expectedHeight = endY - startY;

                            float[,] heightmaps = tData.GetHeights(
                                Mathf.Max(startX, 0),
                                Mathf.Max(startY, 0),
                                Mathf.Min(endX, tData.heightmapResolution) - startX,
                                Mathf.Min(endY, tData.heightmapResolution) - startY
                                );

                            float infPX = influencingPoints.GetLength(0);
                            float infPY = influencingPoints.GetLength(1);

                            int hmX = heightmaps.GetLength(0);
                            int hmY = heightmaps.GetLength(1);

                            //Remove anything in grid
                            //var (gridX, gridY) = LevelData.gridManager.SamplePosition(hitPoint.x, hitPoint.z);
                            //LevelData.gridManager.DestroyInBrush(gridX, gridY);

                            for (int i = 0; i < hmX; i++)
                            {
                                for (int j = 0; j < hmY; j++)
                                {
                                    int influencePointX = (int) Mathf.Clamp((i / expectedWidth + (-Mathf.Min(0, startY) / expectedWidth)) * infPX,  0, infPX - 1);
                                    int influencePointY = (int) Mathf.Clamp((j / expectedHeight + (-Mathf.Min(0, startX) / expectedHeight)) * infPY, 0, infPY - 1);
                                    
                                    //print("I: " + i + " |J: " + j + " |PX: " + influencePointX + " |PY: " + influencePointY + " |HX: " + heightmaps.GetLength(0) + " |HY: " + heightmaps.GetLength(1));
                                    float influenceAmount = influencingPoints[influencePointX, influencePointY] * strength;

                                    if (influenceAmount <= 0.001f)
                                        continue;
                                    var (gridX, gridY) = LevelData.gridManager.SamplePosition(hitPoint.x, hitPoint.z);
                                    LevelData.gridManager.DestroyInBrush(gridX, gridY);

                                    if (editTerrainMode == TerrainEditMode.RAISE_LOWER)
                                    {
                                        heightmaps[i, j] += influenceAmount * strengthSave / 2f * Time.deltaTime;
                                    }
                                    else if (editTerrainMode == TerrainEditMode.FLATTEN_BY_VALUE)
                                    {
                                        //areaT[i, j] = Mathf.Lerp(areaT[i, j], flattenTarget, craterData[i * newTex.width + j].a * strengthNormalized);
                                        if (influenceAmount > 0.5f)
                                            heightmaps[i, j] = height;
                                    }
                                    else if (editTerrainMode == TerrainEditMode.FLATTEN_BY_HEIGHT)
                                    {
                                        if (influenceAmount > 0.5f)
                                            heightmaps[i, j] = lastHeight;
                                    }
                                    else if (editTerrainMode == TerrainEditMode.SMOOTH)
                                    {
                                        if (i == 0 || i == heightmaps.GetLength(0) - 1 || j == 0 || j == heightmaps.GetLength(1) - 1)
                                            continue;

                                        float heightSum = 0;
                                        for (int ySub = -1; ySub <= 1; ySub++)
                                        {
                                            for (int xSub = -1; xSub <= 1; xSub++)
                                            {
                                                heightSum += heightmaps[i + ySub, j + xSub];
                                            }
                                        }

                                        heightmaps[i, j] = Mathf.Lerp(heightmaps[i, j], (heightSum / 9), influenceAmount * 4f * strength);
                                    }
                                }
                            }

                            tData.SetHeights(Mathf.Max(startX, 0), Mathf.Max(startY, 0), heightmaps);
                            break;
                        case Mode.PAINT_TERRAIN:
                            startX = (int)(Mathf.Max(hit.point.x - area, 0) / tData.size.x * tData.alphamapResolution);
                            startY = (int)(Mathf.Max(hit.point.z - area, 0) / tData.size.z * tData.alphamapResolution);
                            endX = (int)(Mathf.Min((hit.point.x + area) / tData.size.x * tData.alphamapResolution, tData.alphamapResolution));
                            endY = (int)(Mathf.Min((hit.point.z + area) / tData.size.z * tData.alphamapResolution, tData.alphamapResolution));

                            float[,,] alphaAreas = tData.GetAlphamaps(startX, startY, endX - startX, endY - startY);
                            for (int i = 0; i < alphaAreas.GetLength(0); i++)
                            {
                                for (int j = 0; j < alphaAreas.GetLength(1); j++)
                                {
                                    for (int at = 0; at < tData.alphamapLayers; at++)
                                    {
                                        int influencePointX = (int)((float)i / alphaAreas.GetLength(0) * influencingPoints.GetLength(0));
                                        int influencePointY = (int)((float)j / alphaAreas.GetLength(1) * influencingPoints.GetLength(1));
                                        float influenceAmount = influencingPoints[influencePointX, influencePointY] * strength;
                                        if (at == selectedTextureIndex)
                                        {
                                            alphaAreas[i, j, at] += influenceAmount;
                                        }
                                        else
                                        {
                                            alphaAreas[i, j, at] -= alphaAreas[i, j, at] * influenceAmount;
                                        }
                                    }
                                }
                            }

                            tData.SetAlphamaps(startX, startY, alphaAreas);
                            break;
                        case Mode.PAINT_DETAILS:
                            startX = (int)(Mathf.Max(hit.point.x - area, 0) / tData.size.x * tData.detailResolution);
                            startY = (int)(Mathf.Max(hit.point.z - area, 0) / tData.size.z * tData.detailResolution);
                            endX = (int)(Mathf.Min((hit.point.x + area) / tData.size.x * tData.detailResolution, tData.detailResolution));
                            endY = (int)(Mathf.Min((hit.point.z + area) / tData.size.z * tData.detailResolution, tData.detailResolution));

                            int[,] details = tData.GetDetailLayer(startX, startY, endX - startX, endY - startY, 0);

                            for (int i = 0; i < details.GetLength(0); i++)
                            {
                                for (int j = 0; j < details.GetLength(1); j++)
                                {
                                    details[i, j] = UnityEngine.Random.Range(0f, 1f) < strength ? 1 : 0;
                                }
                            }

                            tData.SetDetailLayer(startX, startY, 0, details);
                            break;
                    }
                }
            }
        }
    }

    public void changeMode(Mode newMode)
    {
        this.mode = newMode;
    }

    public void changeBrush(Texture2D newBrush)
    {
        //Convert texture2D to alpha float ponts with [0-1] range.
        Color[] brushPixels = newBrush.GetPixels(0, 0, newBrush.width, newBrush.height);
        influencingPoints = new float[newBrush.width, newBrush.height];

        for(int y = 0; y < influencingPoints.GetLength(1); y++)
        {
            for (int x = 0; x < influencingPoints.GetLength(0); x++)
            {
                influencingPoints[x, y] = brushPixels[x + y * newBrush.width].r;
            }
        }

        //Change brush image
        brushImage.GetComponent<MeshRenderer>().material.mainTexture = newBrush;
    }

    public void setBrushSize(int area)
    {
        this.area = area;
        brushSizeTxt.text = "Brush Size [" + area + "]";
        brushImage.transform.localScale = new Vector3(area * buildTargetScaleMultiplier, 1, area * buildTargetScaleMultiplier);
    }

    public void setSelectedTextureId(int id)
    {
        this.selectedTextureIndex = id;
    }

    public void setBrushImageVisibility(bool isActive)
    {
        brushImage.gameObject.SetActive(isActive);
    }
}
