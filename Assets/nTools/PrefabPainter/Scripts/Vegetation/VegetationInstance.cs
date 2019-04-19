/********************************************************************
	created:	18:4:2019   18:14
	filename: 	VegetationInstance.cs
	author:		disen
	des:		植被系统 use gpu instance
	modify::	
*********************************************************************/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VegetationInstance : MonoBehaviour
{
    private System.Action excute_load_start = null;
    private System.Action<int, int> excute_load = null;
    private System.Action excute_load_end = null;

    //lod self check delegate.
    private System.Action<Vector3, float> excute_lod_check = null;
    //excute result
    private System.Action excute_use = null;

    public bool Support_Instance = true;
#if UNITY_EDITOR
    public bool Is_Show_Gizmos = false;
#endif
    public string name;
    //tile info
    private Vector3 offset;
    private int row_count;
    private int column_count;
    private float row_width;
    private float column_length;

    //culling group
    private CullingGroup culling_group;
    private int culling_use_count = 0;
    private BoundingSphere[] culling_bounding_spheres;
    private CullingLodData[,] cullinglod_arr = null;

    //lod
    public float lod_distance = 100f;
    private float lod_max_distance 
    {
        get {
            return lod_distance * lod_distance;
        }
    }
    //lod检测间隔时间
    private float lod_calculate_frequency = 0.5f;
    private float lod_frequency_count = 0f;

    private Transform camera_tran = null;
    private bool inited = false;

    private int changed_count = 0;
    private int changed_trigger_count = 2;
    public bool will_refresh = true;

    //植被数据
    public List<VegetationData> vegetation_data;

    #region internal class
    public class CullingLodData
    {
        public int x;
        public int z;

        public Vector3 position;

        public bool culling = true;
        public bool lod = true;

        private System.Action<int, int> on_use;
        //数据发生变化时,通知
        private System.Action on_changed;

        public CullingLodData(int x, int z, Vector3 pos, System.Action<int, int> use, System.Action change)
        {
            this.x = x;
            this.z = z;
            this.position = pos;
            this.on_use = use;
            this.on_changed = change;
        }

        public void OnExcute()
        {
            if (culling && lod)
            {
                on_use(x, z);
            }
        }

        public void CalculateLod(Vector3 camera_pos, float lod_dis)
        {
            float dis = (camera_pos - position).sqrMagnitude;
            bool result = dis < lod_dis;
            if (lod != result)
            {
                lod = result;

                if (on_changed != null)
                {
                    on_changed();
                }
            }
        }

        public void ChangeCulling(bool result)
        {
            if (culling != result)
            {
                culling = result;

                if (on_changed != null)
                {
                    on_changed();
                }
            }
        }
    }
    [System.Serializable]
    public class VegetationData
    {
        //src
        public string group_name;
        public string prefab_path;
        public MatrixData[,] tile_matrix;

        //use
        public Mesh mesh;
        public Material material;
        public int inst_count = 0;
        private int matrix_index = -1;
        public List<MatrixInstance> matrixes;

        private const int Max_Inst_Count = 500;

        public void Init()
        { 
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(prefab_path))
            {
                GameObject gobj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefab_path);
                if (gobj != null)
                { 
                    MeshFilter mf = gobj.GetComponent<MeshFilter>();
                    if(mf != null)
                    {
                        mesh = mf.sharedMesh;
                    }
                    MeshRenderer mr = gobj.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        material = mr.sharedMaterial;
                    }
                }
            }
#endif
        }

        public void OnLoadStart()
        {
            inst_count = 0;
            matrix_index = 0;
            if(matrixes == null || matrixes.Count == 0)
            {
                matrixes = new List<MatrixInstance>();
                matrixes.Add(new MatrixInstance(Max_Inst_Count));
            }
            for (int i = 0; i < matrixes.Count; i++)
            {
                matrixes[i].use_count = 0;
            }
        }

        public void OnLoad(int x, int z)
        {
            MatrixData data = tile_matrix[x, z];
            if (data != null)
            {
                AddToMatrixList(data.data);
            }
        }

        public void OnLoadEnd()
        {
            for (int i = 1; i < matrixes.Count; i++)
            {
                if (matrixes[i].use_count == 0)
                {
                    matrixes.RemoveAt(i);
                }
            }
        }

        public void DrawMeshInstance()
        {
            for (int i = 0; i <= matrix_index; i++)
            {
                MatrixInstance mi = matrixes[i];
                Graphics.DrawMeshInstanced(mesh, 0, material, mi.data, mi.use_count);
            }
        }

        private void AddToMatrixList(Matrix4x4[] matrix)
        {
            MatrixInstance ml = matrixes[matrix_index];
            if (ml.use_count + matrix.Length < Max_Inst_Count)
            {
                for (int i = 0; i < matrix.Length; i++)
                {
                    ml.data[ml.use_count] = matrix[i];
                    ml.use_count++;
                    inst_count++;
                }
            }
            else
            {
                matrix_index++;
                if (matrixes.Count == matrix_index)
                {
                    ml = new MatrixInstance(Max_Inst_Count);
                    matrixes.Add(ml);
                }
                AddToMatrixList(matrix);
            }
        }
    }
    public class MatrixData
    {
        public Matrix4x4[] data = null;
    }
    [System.Serializable]
    public class MatrixInstance
    {
        public int use_count = 0;
        public Matrix4x4[] data = null;

        public MatrixInstance(int count)
        {
            this.use_count = 0;
            this.data = new Matrix4x4[count];
        }
    }
    #endregion

    // Use this for initialization
    void Start()
    {
        Support_Instance = SystemInfo.supportsInstancing;
        if (Support_Instance)
        {
            Init();
        }
    }

    void OnEnable()
    {
        InitCullingGroup();
    }

    void OnDisable()
    {
        ClearCullingGroup();
    }

    void Init()
    {
        Clear();

        Load();

        InitCullingLodData();

        inited = true;
    }

    void OnRefreshVegetation()
    {
        if (will_refresh)
        {
            will_refresh = false;
            OnVegetationLoadStart();
            OnVegetationLoadExcute();
            OnVegetationLoadEnd();
        }
    }

    #region Culling & Lod
    void InitCullingGroup()
    {
        if (culling_group == null && culling_bounding_spheres != null)
        {
            culling_group = new CullingGroup();
            culling_group.targetCamera = Camera.main;
            culling_group.onStateChanged = OnCullingStateChanged;
            culling_group.SetBoundingSpheres(culling_bounding_spheres);
            culling_group.SetBoundingSphereCount(culling_use_count);
        }
    }

    void ClearCullingGroup()
    {
        if (culling_group != null)
        {
            culling_group.Dispose();
            culling_group = null;
        }
    }

    //初始化culling group  
    void InitCullingLodData()
    {
        culling_use_count = 0;
        culling_bounding_spheres = new BoundingSphere[row_count * column_count];
        InitCullingGroup();
        cullinglod_arr = new CullingLodData[row_count, column_count];
        float radius = (row_width+column_length) * 0.5f + 1f;
        for (int m = 0; m < row_count; m++)
        {
            float z = row_width * 0.5f + m * row_width;
            for (int n = 0; n < column_count; n++)
            {
                //boundingsphere create.
                BoundingSphere bs = new BoundingSphere();
                float x = column_length * 0.5f + n * column_length;
                bs.position = new Vector3(x + offset.x, offset.y, z + offset.z);
                bs.radius = radius;
                culling_bounding_spheres[culling_use_count++] = bs;

                //culling lod data create.
                CullingLodData cl_data = new CullingLodData(m, n, bs.position, this.OnVegetationLoad, this.OnCullingLodStateChanged);
                
                excute_lod_check += cl_data.CalculateLod;
                excute_use += cl_data.OnExcute;

                cullinglod_arr[m, n] = cl_data;
            }
        }
        culling_group.SetBoundingSphereCount(culling_use_count);

        for (int i = 0; i < culling_use_count; i++)
        {
            int x = i / column_count;
            int z = i % column_count;

            cullinglod_arr[x, z].ChangeCulling(culling_group.IsVisible(i));
        }

        OnUpdateLoadCheck();
    }

    //on culling state changed call
    void OnCullingStateChanged(CullingGroupEvent evt)
    {
        if (!inited)
        {
            return;
        }
        int index = evt.index;
        int x = index / column_count;
        int z = index % column_count;

        bool result = false;
        if (evt.hasBecomeVisible)
        {
            result = true;
        }
        if (evt.hasBecomeInvisible)
        {
            result = false;
        }
        cullinglod_arr[x, z].ChangeCulling(result);
    }

    //lod auto check
    void OnUpdateLoadCheck()
    {
        lod_frequency_count += Time.deltaTime;
        if (lod_frequency_count > lod_calculate_frequency)
        {
            if (excute_lod_check != null)
            {
                excute_lod_check(GetCameraPosition(), lod_max_distance);
            }
            lod_frequency_count = 0;
        }
    }

    void OnCullingLodStateChanged()
    {
        changed_count++;
        if (changed_count >= changed_trigger_count)
        {
            will_refresh = true;
            changed_count = 0;
        }
    }

    //get main camera position
    Vector3 GetCameraPosition()
    {
        if (camera_tran == null)
        {
            if (Camera.main != null)
            {
                camera_tran = Camera.main.transform;
            }
        }

        if (camera_tran != null)
        {
            return camera_tran.position;
        }
        return Vector3.zero;
    }
    #endregion

    #region On Vagetation
    void OnVegetationLoadStart()
    {
        if (excute_load_start != null)
        {
            excute_load_start();
        }
    }

    void OnVegetationLoadExcute()
    {
        if (excute_use != null)
        {
            excute_use();
        }
    }

    void OnVegetationLoad(int x, int z)
    {
        if (excute_load != null)
        {
            excute_load(x, z);
        }
    }

    void OnVegetationLoadEnd()
    {
        if (excute_load_end != null)
        {
            excute_load_end();
        }
    }

    void OnDrawMeshInstance()
    {
        for (int i = 0; i < vegetation_data.Count; i++)
        {
            vegetation_data[i].DrawMeshInstance();
        }
    }
    #endregion

    void Clear()
    {
        vegetation_data = new List<VegetationData>();

        excute_lod_check = null;
        excute_load_start = null;
        excute_load = null;
        excute_load_end = null;
        excute_use = null;

        changed_count = 0;
    }

    // Update is called once per frame
    void Update()
    {
        OnUpdateLoadCheck();
        OnRefreshVegetation();
        OnDrawMeshInstance();
    }

    #region Load Json
    private void Load()
    {
        string data_path = name;
        string json_str = Resources.Load<TextAsset>(data_path).text;
        LoadJson(json_str);
    }

    private void LoadJson(string json_str)
    {
        Hashtable table = (Hashtable)ArtTool.SimpleJsonUtil.jsonDecode(json_str);

        string[] vec3 = table["offset"].ToString().Split("|"[0]);
        this.offset = new Vector3(float.Parse(vec3[0]), float.Parse(vec3[1]), float.Parse(vec3[2]));
        this.row_count = System.Convert.ToInt32(table["row_count"]);
        this.column_count = System.Convert.ToInt32(table["column_count"]);
        this.row_width = System.Convert.ToInt32(table["row_width"]);
        this.column_length = System.Convert.ToInt32(table["column_length"]);

        ArrayList arr = (ArrayList)(table["objs"]);
        this.vegetation_data = new List<VegetationData>();
        for (int i = 0; i < arr.Count; i++)
        {
            Hashtable arr_table = (Hashtable)arr[i];
            VegetationData info = new VegetationData();
            info.group_name = arr_table["group_name"].ToString();
            info.prefab_path = arr_table["prefab_path"].ToString();
            info.Init();

            excute_load_start += info.OnLoadStart;
            excute_load += info.OnLoad;
            excute_load_end += info.OnLoadEnd;

            info.tile_matrix = new MatrixData[row_count, column_count];
            Hashtable objs_table = (Hashtable)arr_table["objs_dic"];
    
            IDictionaryEnumerator e = objs_table.GetEnumerator();
            while (e.MoveNext())
            {
                int key = System.Convert.ToInt32(e.Key);
                //101 -> 1,1
                int x = key / 100;
                int z = key % 100;

                MatrixData matrix_data = new MatrixData();
                Hashtable value = (Hashtable)e.Value;
                matrix_data.data = new Matrix4x4[value.Count];
                IDictionaryEnumerator ve = value.GetEnumerator();
                int index = 0;
                while (ve.MoveNext())
                {
                    ArrayList m_arr = (ArrayList)ve.Value;
                    Matrix4x4 tran_matrix = new Matrix4x4();
                    Vector3 tran_pos = new Vector3(
                        System.Convert.ToInt32(m_arr[0])*0.001f,
                        System.Convert.ToInt32(m_arr[1])*0.001f,
                        System.Convert.ToInt32(m_arr[2])*0.001f
                        );
                    Quaternion tran_rot = Quaternion.Euler(
                        System.Convert.ToInt32(m_arr[3]) * 0.001f,
                        System.Convert.ToInt32(m_arr[4]) * 0.001f,
                        System.Convert.ToInt32(m_arr[5]) * 0.001f
                        );
                    Vector3 tran_scale = new Vector3(
                        System.Convert.ToInt32(m_arr[6]) * 0.001f,
                        System.Convert.ToInt32(m_arr[7]) * 0.001f,
                        System.Convert.ToInt32(m_arr[8]) * 0.001f
                        );
  
                    tran_matrix.SetTRS(tran_pos, tran_rot, tran_scale);
                    matrix_data.data[index++] = tran_matrix;
                }
                if (x >= row_count || z >= column_count)
                {
                    Debug.Log(key.ToString());
                }
                info.tile_matrix[x, z] = matrix_data;
            }
            this.vegetation_data.Add(info);
        }
    }

    #endregion

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Is_Show_Gizmos)
        {
            return;
        }
        for (int x = 0; x < row_count; x++)
        {
            for (int z = 0; z < column_count; z++)
            {
                CullingLodData data = cullinglod_arr[x, z];
                if (data.culling && data.lod)
                {
                    Gizmos.color = Color.green;
                }
                else {
                    Gizmos.color = Color.red;
                }
                Gizmos.DrawWireCube(data.position, new Vector3(column_length, 1, row_width));
            }
        }
    }
#endif
}
