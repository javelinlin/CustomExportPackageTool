// jave.lin 2021/04/07

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class FileOptInfo
{
	public bool export;
	public string path;
	public UnityEngine.Object obj;
}

public class ClipboardImportInfo
{
	public bool import;
	public string path;
}

public class CustomExportPackageClipboardDialog : EditorWindow
{
    private string ignorePrefixStr = "https://192.168.x.x:xxx/xx/xxx/xxx/xxx/xxx/";
	private string lastIgnorePrefixStr;
	private Vector2 scrollViewPos;

	private string clipboardStr;
	private List<string> srcLines = new List<string>();
	private List<ClipboardImportInfo> filterImportInfoList = new List<ClipboardImportInfo>();
	private List<string> filterLines = new List<string>();

	public static List<string> ShowDialog(EditorWindow parent)
    {
#if UNITY_2019_4_OR_NEWER

		var win = EditorWindow.GetWindow<CustomExportPackageClipboardDialog>();
		var w = parent.position.width * 0.5f;
		var h = parent.position.height * 0.5f;
		var x = parent.position.x + (w) * 0.5f;
		var y = parent.position.y + (h) * 0.5f;
		var rect = new Rect(x, y, w, h);
		//var win = EditorWindow.GetWindowWithRect(
		//	typeof(CustomExportPackageClipboardDialog), 
		//	rect, false, "Filter Clipboard Data")
		//	as CustomExportPackageClipboardDialog; // rect 参数这是没有效果

		win.clipboardStr = GUIUtility.systemCopyBuffer;
		var lines = win.clipboardStr.Split(new string[] { "\n\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
			var tempLine = line.Trim();
			if (string.IsNullOrEmpty(tempLine))
			{
				continue;
			}
			win.srcLines.Add(line);
        }

		win.position = rect; // 上面的 GetWindowWithRect 中的 rect 参数没有效果，只能在 EditorWindow.position 上设置才有效果
		win.titleContent = new GUIContent("过滤剪贴板内容");
		win.ShowModal();
		return win.GetFilterPaths();
#else
		return new List<string>();
#endif
	}

	public List<string> GetFilterPaths()
    {
		filterLines.Clear();
        foreach (var info in filterImportInfoList)
        {
			if (info.import) filterLines.Add(info.path);
        }
		return filterLines;
    }

    private void OnGUI()
    {
		//if (GUILayout.Button("OK"))
		//{
		//	this.Close();
		//	return;
		//}

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Ignore Prefix : ", GUILayout.Width(100));
		ignorePrefixStr = EditorGUILayout.TextField(ignorePrefixStr);
		EditorGUILayout.EndHorizontal();

		scrollViewPos = EditorGUILayout.BeginScrollView(scrollViewPos);

		if (lastIgnorePrefixStr != ignorePrefixStr)
		{
			lastIgnorePrefixStr = ignorePrefixStr;
			filterLines.Clear();

			foreach (var line in srcLines)
			{
				var newLineContent = line;
				if (!string.IsNullOrEmpty(ignorePrefixStr) && line.StartsWith(ignorePrefixStr))
				{
					newLineContent = line.Substring(ignorePrefixStr.Length);
				}
				newLineContent = newLineContent.Trim();
				if (!string.IsNullOrEmpty(newLineContent))
				{
					filterLines.Add(newLineContent);
				}
			}
			if (filterImportInfoList.Count == 0)
			{
				foreach (var line in filterLines)
				{
					filterImportInfoList.Add(new ClipboardImportInfo { import = true, path = line });
				}
			}
			else
			{
				for (int i = 0; i < filterLines.Count; i++)
				{
					var info = filterImportInfoList[i];
					info.path = filterLines[i];
				}
			}
		}

		EditorGUILayout.BeginVertical();
		foreach (var info in filterImportInfoList)
		{
			EditorGUILayout.BeginHorizontal();
			info.import = EditorGUILayout.Toggle(info.import, GUILayout.Width(12));
			EditorGUILayout.TextField(info.path);
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndVertical();

		EditorGUILayout.EndScrollView();
	}
}

public class CustomExportPackageWindow : EditorWindow
{
	public static string[] clipboardAssetPaths;

	private Vector2 dragDropFileScrollViewPos;
	private Vector2 allExportFileScrollViewPos;

	private List<FileOptInfo> fileOptInfoList = new List<FileOptInfo>();
	private List<FileOptInfo> allExportFileOptList = new List<FileOptInfo>();

	private List<string> pushList = new List<string>();

	private Action OnRefreshDepsEvent;

	public string defaultExportPath = null; // "Assets/ExportUnityPackage";
	public bool useDefaultExportPath = false;

	[MenuItem("实用工具/资源工具/自定义的资源导出工具")]
    public static void _Show()
    {
        var win = EditorWindow.GetWindow<CustomExportPackageWindow>();
		win.titleContent = new GUIContent("自定义的资源导出工具");
		win.Init();
        win.Show();
    }

	private void Init()
    {
		OnRefreshDepsEvent = OnRefreshDepsEventCallback;
	}

	private void OnRefreshDepsEventCallback()
    {
		RefreshDeps();
    }

	private void HandlePushList()
    {
		if (pushList.Count > 0)
        {
			var havePushSuccess = false;
            foreach (var path in pushList)
            {
				var tempPath = path.Trim();
				if (string.IsNullOrEmpty(tempPath))
                {
					continue;
                }
				if (AddPath(path))
                {
					havePushSuccess = true;
				}
			}
			pushList.Clear();

			if (havePushSuccess && OnRefreshDepsEvent != null) OnRefreshDepsEvent.Invoke();
		}
    }

	private void OnGUI()
    {
		///
		/// Handle Push List
		///
		HandlePushList();

		///
		/// Handle Clipboard Data
		///
		HandleClipboardData();

		///
		/// Drag and Drop
		///
		DragDrop();

		EditorGUILayout.BeginHorizontal();
		///
		/// Display Files
		///
		DisplayFileOptList();

		///
		/// Display All Dependencies
		///
		DisplayAllDependencies();
		EditorGUILayout.EndHorizontal();
	}

	private void HandleClipboardData()
    {
		if (Event.current.control && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.V)
        {
			if (!string.IsNullOrEmpty(GUIUtility.systemCopyBuffer))
			{
				var paths = CustomExportPackageClipboardDialog.ShowDialog(this);
				if (paths != null && paths.Count > 0)
                {
                    foreach (var path in paths)
                    {
						pushList.Add(path);
					}
				}
			}
		}
	}

	public void RefreshDeps()
    {
		HashSet<string> distincSet = new HashSet<string>();
		foreach (var opt in fileOptInfoList)
		{
			if (!opt.export) continue;

			var deps = AssetDatabase.GetDependencies(opt.path.Trim());

			foreach (var dep in deps)
			{
				if (!distincSet.Contains(dep))
				{
					distincSet.Add(dep);
				}
			}
		}

		var depArr = new string[distincSet.Count];
		distincSet.CopyTo(depArr);

		Array.Sort(depArr);

		// Record src export value
		Dictionary<string, bool> srcExportDict = new Dictionary<string, bool>();
        foreach (var opt in allExportFileOptList)
        {
			srcExportDict[opt.path] = opt.export;
        }

		allExportFileOptList.Clear();
		foreach (var dep in depArr)
		{
            // Try recovery src export value
            bool srcExportValue;
            if (!srcExportDict.TryGetValue(dep, out srcExportValue))
            {
				// If not found, default export value EQUALS : true
				srcExportValue = true;
			}
			allExportFileOptList.Add(new FileOptInfo { export = srcExportValue, path = dep });
		}
	}

	private void DisplayFileOptList()
    {
		EditorGUILayout.BeginVertical();

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Clear"))
		{
			fileOptInfoList.Clear();
			allExportFileOptList.Clear();
		}
		if (GUILayout.Button("Refresh Deps"))
		{
			if (OnRefreshDepsEvent != null)
                OnRefreshDepsEvent.Invoke();
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.LabelField("DragDrop Files:");

		dragDropFileScrollViewPos = EditorGUILayout.BeginScrollView(dragDropFileScrollViewPos/*, GUILayout.Width(300), GUILayout.Width(300)*/);

		var refreshDepsEventFire = false;
		for (int i = 0; i < fileOptInfoList.Count; i++)
		{
			var remove = false;
			var info = fileOptInfoList[i];
			EditorGUILayout.BeginHorizontal();
			bool srcExportValue = info.export;
			info.export = EditorGUILayout.Toggle(info.export, GUILayout.Width(12));
			if (srcExportValue != info.export)
			{
				refreshDepsEventFire = true;
			}
			remove = GUILayout.Button("X", GUILayout.Width(20));
			if (info.obj == null)
			{
				info.obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.path);
			}
			EditorGUILayout.ObjectField(info.obj, typeof(UnityEngine.Object), false, GUILayout.Width(40));
			EditorGUILayout.TextField(info.path);
			EditorGUILayout.EndHorizontal();
			if (remove)
			{
				fileOptInfoList.RemoveAt(i);
				--i;
				refreshDepsEventFire = true;
			}
		}
		if (refreshDepsEventFire)
		{
			if (OnRefreshDepsEvent != null)
                OnRefreshDepsEvent.Invoke();
		}
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();
    }

	private void DisplayAllDependencies()
	{
		EditorGUILayout.BeginVertical();

		EditorGUILayout.BeginHorizontal();

		EditorGUILayout.LabelField("All Export Files:");

		if (GUILayout.Button("Export", GUILayout.Width(100)))
        {
            Export();
        }
        EditorGUILayout.EndHorizontal();

		allExportFileScrollViewPos = EditorGUILayout.BeginScrollView(allExportFileScrollViewPos/*, GUILayout.Width(300), GUILayout.Width(300)*/);
		foreach (var info in allExportFileOptList)
		{
			EditorGUILayout.BeginHorizontal();
			info.export = EditorGUILayout.Toggle(info.export, GUILayout.Width(12));
			if (info.obj == null)
			{
				info.obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.path);
			}
			EditorGUILayout.ObjectField(info.obj, typeof(UnityEngine.Object), false, GUILayout.Width(40));
			EditorGUILayout.TextField(info.path);
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();
	}

	private void RefreshDefaultExportPath()
    {
		if (string.IsNullOrEmpty(defaultExportPath))
		{
			var dir = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
			defaultExportPath = Path.Combine(dir, "ExportUnityPackage");
		}
	}

    public void Export()
    {
		RefreshDefaultExportPath();

		if (allExportFileOptList.Count > 0)
        {
            var exportCount = 0;
            foreach (var opt in allExportFileOptList)
            {
                if (opt.export) exportCount++;
            }
            if (exportCount > 0)
            {
                var title = "Save Export Unity Package";
                var now = DateTime.Now;
                var defaultName = string.Format(
                    "{0}_{1}_{2}_{3}_{4}_{5}_ToPGUnityPackage",
                    now.Year,
                    now.Month,
                    now.Day,
                    now.Hour,
                    now.Minute,
                    now.Second
                    );
                var extension = "unitypackage";
				string savePath;
				if (useDefaultExportPath)
                {
					savePath = defaultExportPath;
				}
				else
                {
					savePath = EditorUtility.SaveFilePanel(title, Application.dataPath, defaultName, extension);
				}
				if (!string.IsNullOrEmpty(savePath))
                {
                    var dirPath = Path.GetDirectoryName(savePath);
					if (!Directory.Exists(dirPath))
                    {
						Directory.CreateDirectory(dirPath);
					}
                    var exports = new string[exportCount];
                    var idx = 0;
                    foreach (var opt in allExportFileOptList)
                    {
                        if (opt.export)
                        {
                            exports[idx++] = opt.path.Trim();
                        }
                    }
                    AssetDatabase.ExportPackage(exports, savePath, ExportPackageOptions.Default);
                    //System.Diagnostics.Process.Start("explorer.exe", dirPath);

					// 打开并选中对应的文件
                    //Debug.LogError($"dirPath : {dirPath}");
                    var selectPath = savePath.Replace("/", "\\");
                    //Debug.LogError($"select : {selectPath}");
                    System.Diagnostics.Process.Start("explorer.exe", "/select," + selectPath);
                }
            }
        }
    }

    private void DragDrop()
	{
		Event evt = Event.current;
		switch (evt.type)
		{
			case EventType.DragUpdated:
			case EventType.DragPerform:

			DragAndDrop.visualMode = DragAndDropVisualMode.Link;

			if (evt.type == EventType.DragPerform)
			{
				// method 1: handle by objs
				//var objList = new List<UnityEngine.Object>();
				//GetObjsOnDragDrops(DragAndDrop.objectReferences, objList);

				//foreach (UnityEngine.Object draggedObj in objList)
				//{
				//	DoDropObj(draggedObj);
				//}

				// method 2 : handle by obj paths
				var projectViewSelectedObjsPaths = new List<string>();
				TransToPath(DragAndDrop.objectReferences, projectViewSelectedObjsPaths);

				var allProjectViewSelectedObjPaths = new List<string>();
				GetAllObjPathsWithPaths(projectViewSelectedObjsPaths, allProjectViewSelectedObjPaths);

				pushList.AddRange(allProjectViewSelectedObjPaths);
			}
			break;
		}
	}

	private void GetObjsOnDragDrops(UnityEngine.Object[] objs, List<UnityEngine.Object> ret)
    {
		foreach (UnityEngine.Object obj in objs)
		{
			var assetPath = AssetDatabase.GetAssetPath(obj);
			if (JudgeIsAFolder(assetPath))
			{
				var objGUIDs = AssetDatabase.FindAssets("t:Object", new string[] { assetPath });
				var newObjs = new UnityEngine.Object[objGUIDs.Length];
                for (int i = 0; i < objGUIDs.Length; i++)
                {
					var objPath = AssetDatabase.GUIDToAssetPath(objGUIDs[i]);
					newObjs[i] = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(objPath);
                }
				GetObjsOnDragDrops(newObjs, ret);
			}
			else
			{
				ret.Add(obj);
			}
		}
	}

	private void DoDropObj(UnityEngine.Object dragObj)
    {
		pushList.Add(AssetDatabase.GetAssetPath(dragObj));
	}

	private void DoDropPath(string path)
    {
		pushList.Add(path);
	}

	private void TransToPath(UnityEngine.Object[] objs, List<string> objPaths)
    {
        foreach (var obj in objs)
        {
			objPaths.Add(AssetDatabase.GetAssetPath(obj));
		}
    }

	private void GetAllObjPathsWithPaths(List<string> paths, List<string> allObjPaths)
    {
        foreach (var path in paths)
        {
			if (JudgeIsAFolder(path))
			{
				var objGUIDs = AssetDatabase.FindAssets("t:Object", new string[] { path });
				var newPaths = new List<string>();
                foreach (var objGUID in objGUIDs)
                {
					var objPath = AssetDatabase.GUIDToAssetPath(objGUID);
					newPaths.Add(objPath);
				}
				GetAllObjPathsWithPaths(newPaths, allObjPaths);
			}
			else
            {
				allObjPaths.Add(path);
			}
		}
	}

	private bool JudgeIsAFolder(string assetPath)
    {
		return Directory.Exists(assetPath);
    }

	private void DoDropFolder(string path)
    {

    }

	public bool AddPath(string path)
    {
		if (string.IsNullOrEmpty(path))
		{
			Debug.LogError("CustomExportPackageWindow.DoDrop path is null.");
			return false;
		}
		if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)))
        {
			Debug.LogError("CustomExportPackageWindow.DoDrop path is invalidated");
			return false;
		}

		Debug.Log("Add File : " + path);

		var found = false;
		for (int i = 0; i < fileOptInfoList.Count; i++)
		{
			var info = fileOptInfoList[i];
			if (info.path == path)
			{
				found = true;
				break;
			}
		}
		if (!found)
		{
			fileOptInfoList.Add(new FileOptInfo { export = true, path = path });
		}
		return !found;
	}
}

