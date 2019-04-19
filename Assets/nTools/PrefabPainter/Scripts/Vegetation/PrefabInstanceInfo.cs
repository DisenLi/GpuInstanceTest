/********************************************************************
	created:	18:4:2019   19:34
	filename: 	PrefabInstanceInfo.cs
	author:		disen
	des:		保存预设数据用信息类
	modify::	
*********************************************************************/

using UnityEngine;
using System.Collections;

using System.Collections.Generic;
using ArtTool;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PrefabInstanceInfo
{
    public Vector3 offset;//偏移量 
    public int row_count;//行数->z
    public int column_count;//列数->x

    public float row_width;//行宽->z
    public float column_length;//列宽->x
    public List<PrefabObjInfo> objs = new List<PrefabObjInfo>();

#if UNITY_EDITOR
    public void AddGroup(Transform tran)
    {
        PrefabObjInfo info = new PrefabObjInfo();
        info.group_name = tran.name;
        if (tran.childCount > 0)
        {
            //查询对应的预设源文件.
            Transform first = tran.GetChild(0);
            GameObject source = (GameObject)PrefabUtility.GetPrefabParent(first.gameObject);
            string path = AssetDatabase.GetAssetPath(source);
            info.prefab_path = path;

            for (int i = 0; i < tran.childCount; i++)
            {
                Transform child = tran.GetChild(i);
                //计算原始位置(0,0)起
                Vector3 child_pos = child.position - offset;
                //计算位置
                int z = (int)(child_pos.x / column_length);
                int x = (int)(child_pos.z / row_width);
                if (x > -1 && x < row_count && z > -1 && z < column_count)
                {
                    info.AddGObj(x, z, child);
                }
                else 
                {
                    Debug.Log("Not In:"+info.group_name + "  index:"+ i);
                }
            }
            objs.Add(info);
        }
#endif
    }

    public Hashtable ToHashTable()
    {
        Hashtable table = new Hashtable();
        table["offset"] = string.Format("{0}|{1}|{2}", offset.x, offset.y, offset.z);
        table["row_count"] = this.row_count;
        table["column_count"] = this.column_count;
        table["row_width"] = this.row_width;
        table["column_length"] = this.column_length;


        Hashtable[] objs_table = new Hashtable[objs.Count];
        for (int i = 0; i < objs.Count; i++)
        {
            objs_table[i] = objs[i].ToHashTable();
        }
        table["objs"] = objs_table;

        return table;
    }

    public void LoadHashTable(Hashtable table)
    {
        string[] vec3 = table["offset"].ToString().Split("|"[0]);
        this.offset = new Vector3(float.Parse(vec3[0]), float.Parse(vec3[1]), float.Parse(vec3[2]));
        this.row_count = System.Convert.ToInt32(table["row_count"]);
        this.column_count = System.Convert.ToInt32(table["column_count"]);
        this.row_width = System.Convert.ToInt32(table["row_width"]);
        this.column_length = System.Convert.ToInt32(table["column_length"]);

        ArrayList arr = (ArrayList)(table["objs"]);
        this.objs = new List<PrefabObjInfo>();
        for (int i = 0; i < arr.Count; i++)
        {
            Hashtable arr_table = (Hashtable)arr[i];
            PrefabObjInfo info = new PrefabObjInfo();
            info.LoadHashTable(arr_table);
            this.objs.Add(info);
        }
    }
}

public class PrefabObjInfo
{
    public string group_name;//组名称
    public string prefab_path;//使用的预设资源路径,加载用
    //位置信息
    public Dictionary<int, List<int[]>> objs_dic = new Dictionary<int, List<int[]>>();

    public void AddGObj(int x, int z, Transform tran)
    {
        int key = x * 100 + z;
        if (!objs_dic.ContainsKey(key))
        {
            objs_dic.Add(key, new List<int[]>());
        }
        List<int[]> matrix_list = objs_dic[key];
        int[] tran_arr = new int[10];
        tran_arr[0] = FormatFloat(tran.position.x);//t
        tran_arr[1] = FormatFloat(tran.position.y);
        tran_arr[2] = FormatFloat(tran.position.z);
        tran_arr[3] = FormatFloat(tran.eulerAngles.x);//r
        tran_arr[4] = FormatFloat(tran.eulerAngles.y);
        tran_arr[5] = FormatFloat(tran.eulerAngles.z);
        tran_arr[6] = FormatFloat(tran.localScale.x);//s
        tran_arr[7] = FormatFloat(tran.localScale.y);
        tran_arr[8] = FormatFloat(tran.localScale.z);
        matrix_list.Add(tran_arr);
    }

    int FormatFloat(float v)
    {
        return Mathf.RoundToInt(v * 1000f);
    }

    public Hashtable ToHashTable()
    {
        Hashtable table = new Hashtable();
        table["group_name"] = this.group_name;
        table["prefab_path"] = this.prefab_path;

        Hashtable objs_table = new Hashtable();
        Dictionary<int, List<int[]>>.Enumerator iter = objs_dic.GetEnumerator();
        while (iter.MoveNext())
        {
            int key = iter.Current.Key;
            List<int[]> value = iter.Current.Value;

            Hashtable list = new Hashtable();
            for (int i = 0; i < value.Count; i++)
            {
                list.Add(i, value[i]);
            }
            objs_table.Add(key, list);
        }

        table["objs_dic"] = objs_table;

        return table;
    }

    public void LoadHashTable(Hashtable table)
    {
        this.group_name = table["group_name"].ToString();
        this.prefab_path = table["prefab_path"].ToString();

        Hashtable objs_table = (Hashtable)table["objs_dic"];
        this.objs_dic = new Dictionary<int, List<int[]>>();
        IDictionaryEnumerator e = objs_table.GetEnumerator();
        while (e.MoveNext())
        {
            int key = System.Convert.ToInt32(e.Key);
            Hashtable value = (Hashtable)e.Value;

            IDictionaryEnumerator ve = value.GetEnumerator();
            List<int[]> trans = new List<int[]>();
            while (ve.MoveNext())
            {
                ArrayList arr = (ArrayList)ve.Value;
                int[] tran_point = new int[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                {
                    tran_point[i] = System.Convert.ToInt32(arr[i]);
                }
                trans.Add(tran_point);
            }

            this.objs_dic.Add(key, trans);
        }
    }
}