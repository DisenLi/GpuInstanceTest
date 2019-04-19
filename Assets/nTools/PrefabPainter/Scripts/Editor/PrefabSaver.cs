using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

using ArtTool;
namespace nTools.PrefabPainter
{
    public class PrefabPainterSaver
    {
        public static void Painter_PrefabSave(GameObject land)
        {
            if (land == null)
            {
                return;
            }
            PrefabPainterSettings settings = PrefabPainterSettings.current;
            if(settings != null)
            {
                PrefabInstanceInfo info = new PrefabInstanceInfo();
                info.offset = settings.tile_offset;
                info.row_count = settings.tile_row;
                info.column_count = settings.tile_column;
                info.row_width = settings.tile_width;
                info.column_length = settings.tile_length;

                for (int i = 0; i < land.transform.childCount; i++)
                {
                    Transform child = land.transform.GetChild(i);
                    info.AddGroup(child);
                }
                Hashtable table = info.ToHashTable();
                string str = SimpleJsonUtil.jsonEncode(table);

                string path = EditorUtility.SaveFilePanel("植被数据保存", Application.dataPath, "land", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, str);
                    EditorUtility.DisplayDialog("提示", "保存成功", "确定");
                }
            }
        }

        public static GameObject LoadPrefabInfo(out PrefabInstanceInfo pi_info)
        {
            GameObject result = null;
            pi_info = null;
            string path = EditorUtility.OpenFilePanel("植被数据读取", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            string content = System.IO.File.ReadAllText(path);
            if (content != null)
            {
                Hashtable info = (Hashtable)SimpleJsonUtil.jsonDecode(content);
                pi_info = new PrefabInstanceInfo();
                pi_info.LoadHashTable(info);

                GameObject land = new GameObject("land");
                result = land;
                Transform land_tran = land.transform;
                land_tran.position = Vector3.zero;

                for (int i = 0; i < pi_info.objs.Count; i++)
                {
                    PrefabObjInfo po_info = pi_info.objs[i];
                    GameObject group = new GameObject(po_info.group_name);
                    Transform group_tran = group.transform;
                    group_tran.parent = land_tran;
                    group_tran.position = Vector3.zero;
                    group_tran.rotation = Quaternion.identity;
                    group_tran.localScale = Vector3.one;

                    GameObject src_obj = AssetDatabase.LoadAssetAtPath<GameObject>(po_info.prefab_path);

                    Dictionary<int, List<int[]>>.Enumerator iter = po_info.objs_dic.GetEnumerator();

                    while(iter.MoveNext())
                    {
                        List<int[]> value = iter.Current.Value;
                        for (int j = 0; j < value.Count; j++)
                        {
                            int[] tran_arr = value[j];
                            GameObject dst = (GameObject)PrefabUtility.InstantiatePrefab(src_obj);
                            dst.name = src_obj.name;
                            dst.transform.parent = group_tran;

                            Vector3 position = new Vector3(
                                IntToFloat(tran_arr[0]), 
                                IntToFloat(tran_arr[1]), 
                                IntToFloat(tran_arr[2]));
                            Quaternion rotation = Quaternion.Euler(
                                IntToFloat(tran_arr[3]), 
                                IntToFloat(tran_arr[4]), 
                                IntToFloat(tran_arr[5]));
                            Vector3 scale = new Vector3(
                                IntToFloat(tran_arr[6]),
                                IntToFloat(tran_arr[7]),
                                IntToFloat(tran_arr[8]));
                            dst.transform.position = position;
                            dst.transform.rotation = rotation;
                            dst.transform.localScale = scale;
                        }
                    }
                }

                EditorUtility.DisplayDialog("提示", "读取成功,已经初始化到场景内!", "确定");
            }

            return result;
        }

        static float IntToFloat(int v)
        {
            return v * 0.001f;
        }
    }
}