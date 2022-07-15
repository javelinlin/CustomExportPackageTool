# CustomExportPackageTool
Unity Export Package 有引用统计的 BUG，会导出一堆不相关的东西，所以在 1 年前，写过一个导出 package 工具，使用更方便：还可以中断添加、删除需要导出的资源向

---
See Blog : [Unity - 搬砖日志 - CustomExportPackage - 自定义的资源导出导入工具 - AssetDatabase.ExportPackage](https://blog.csdn.net/linjf520/article/details/115493280)

---
# Roadmap
- [ ] 添加对 *.shader, *.cginc, *.hlsl, *.glsl 格式文件添加 include 的 shader 代码解析关联导入，部分 package local 化，或是 builit 内置化都将不关联导出
