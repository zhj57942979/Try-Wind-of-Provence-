/*
               #########                       
              ############                     
              #############                    
             ##  ###########                   
            ###  ###### #####                  
            ### #######   ####                 
           ###  ########## ####                
          ####  ########### ####               
         ####   ###########  #####             
        #####   ### ########   #####           
       #####   ###   ########   ######         
      ######   ###  ###########   ######       
     ######   #### ##############  ######      
    #######  #####################  ######     
    #######  ######################  ######    
   #######  ###### #################  ######   
   #######  ###### ###### #########   ######   
   #######    ##  ######   ######     ######   
   #######        ######    #####     #####    
    ######        #####     #####     ####     
     #####        ####      #####     ###      
      #####       ###        ###      #        
        ###       ###        ###               
         ##       ###        ###               
__________#_______####_______####______________
                我们的未来没有BUG                
* ==============================================================================
* Filename: StartUp
* Created:  2018/7/2 11:36:16
* Author:   エル・プサイ・コングリィ
* Purpose:  
* ==============================================================================
*/

using UPR.Cecil;
using UPR.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace UPRLuaProfiler
{
    [InitializeOnLoad]
    public static class StartUp
    {
        //private static int tickNum = 0;
        static StartUp()
        {

            /*
            if (LuaDeepProfilerSetting.Instance.isDeepMonoProfiler)
            {
                InjectMethods.InjectAllMethods();
            }
            */
#if XLUA || TOLUA || SLUA
            if (LuaDeepProfilerSetting.Instance.isInited) return;
#endif
            string[] paths = Directory.GetFiles(Application.dataPath, "*.dll", SearchOption.AllDirectories);
            foreach (var item in paths)
            {
                string fileName = Path.GetFileName(item);
                if (fileName == "slua.dll")
                {
                    AppendMacro("#define SLUA");
                }

                if (fileName == "xlua.dll")
                {
                    AppendMacro("#define XLUA");
                    break;
                }

                if (fileName == "tolua.dll")
                {
                    AppendMacro("#define TOLUA");
                    break;
                }
            }

            LuaDeepProfilerSetting.Instance.isInited = true;
        }

         private static void AppendMacro(string macro)
        {
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
            System.Diagnostics.StackFrame sf = st.GetFrame(0);
            string path = sf.GetFileName();
            string selfPath = path;

#if UNITY_EDITOR_WIN
            path = path.Replace("Editor\\StartUp.cs", "Core\\Driver\\LuaDLL.cs");
#else
            path = path.Replace("Editor/StartUp.cs", "Core/Driver/LuaDLL.cs");
#endif
            AppendText(macro, selfPath);
            AppendText(macro, path);
        }

        private static void AppendText(string macro, string path)
        {
            string text = File.ReadAllText(path);
            string text2 = new StringReader(text).ReadLine();
            if (text2.Contains("#define"))
            {
                text = text.Substring(text2.Length, text.Length - text2.Length);
            }
            else
            {
                macro += "\r\n";
            }
            text = text.Insert(0, macro);
            File.WriteAllText(path, text);
        }
    }

    public static class InjectMethods
    {
        private static MethodDefinition m_beginSampleMethod;
        private static MethodDefinition m_endSampleMethod;
        //private static bool m_useLuaProfiler = false;
       
        #region try finally
        public static void InjectAllMethods()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("is compiling");
                return;
            }

            var projectPath = System.Reflection.Assembly.Load("Assembly-CSharp").ManifestModule.FullyQualifiedName;
            var profilerPath = (typeof(LuaProfiler).Assembly).ManifestModule.FullyQualifiedName;
            
            InjectAllMethods(projectPath, profilerPath);
        }

        public static void InjectAllMethods(string path)
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("is compiling");
                return;
            }
            InjectAllMethods(path, path);
        }
        private static bool IsMonoBehavior(TypeDefinition td)
        {
            if (td == null) return false;

            if (td.FullName == "UnityEngine.MonoBehaviour")
            {
                return true;
            }
            else
            {
                if (td.BaseType == null)
                {
                    return false;
                }
                else
                {
                    return IsMonoBehavior(td.BaseType.Resolve());
                }
            }
        }

        private static void InjectAllMethods(string injectPath, string profilerPath)
        {
            string md5 = null;
            md5 = new FileInfo(injectPath).LastWriteTimeUtc.Ticks.ToString();
            if (md5 == LuaDeepProfilerSetting.Instance.assMd5) return;

            AssemblyDefinition injectAss = LoadAssembly(injectPath);
            AssemblyDefinition profilerAss = null;
            if (injectPath == profilerPath)
            {
                profilerAss = injectAss;
            }
            else
            {
                profilerAss = LoadAssembly(profilerPath);
            }
            var profilerType = profilerAss.MainModule.GetType("UPRLuaProfiler.LuaProfiler");
            if (profilerType == null)
            {
                Debug.LogWarning("Can't load LuaProfiler, Mono is recommended for building.");
            }
            else
            {
                foreach (var m in profilerType.Methods)
                {
                    if (m.Name == "BeginSampleCSharp")
                    {
                        m_beginSampleMethod = m;
                    }
                    if (m.Name == "EndSampleCSharp")
                    {
                        m_endSampleMethod = m;
                    }
                }
            }
            
            var module = injectAss.MainModule;
            foreach (var type in injectAss.MainModule.Types)
            {
                string name = "";
                if (type.FullName.Contains("UPRLuaProfiler"))
                {
                    continue;
                }
                if (type.FullName.Contains("MikuLuaProfiler"))
                {
                    continue;
                }

                if (type.FullName.Contains("UPRProfiler"))
                    name = "UPR ";
                foreach (var item in type.Methods)
                {
                    //丢弃协同 
                    if (item.ReturnType.Name.Contains("IEnumerator"))
                    {
                        continue;
                    }

                    if (item.Name == ".cctor")
                    {
                        continue;
                    }

                    if (item.Name == ".ctor")
                    {
                        if (item.DeclaringType.IsSerializable)
                        {
                            continue;
                        }
                        bool isMonoBehaviour = IsMonoBehavior(item.DeclaringType.BaseType.Resolve());
                        if (isMonoBehaviour)
                        {
                            continue;
                        } 
                    }

                    if (item.IsAbstract)
                    {
                        continue;
                    }
                    if (item.IsPInvokeImpl)
                    {
                        continue;
                    }
                    if (item.Body == null)
                    {
                        continue;
                    }

                    InjectTryFinally(item, module, name);
                }
            }

            WriteAssembly(injectPath, injectAss);
            LuaDeepProfilerSetting.Instance.assMd5 = new FileInfo(injectPath).LastWriteTimeUtc.Ticks.ToString();
        }



        private static string GetReflectionName(this TypeReference type)
        {
            if (type.IsGenericInstance)
            {
                var genericInstance = (GenericInstanceType)type;
                return string.Format("{0}.{1}[{2}]", genericInstance.Namespace, type.Name, String.Join(",", genericInstance.GenericArguments.Select(p => p.GetReflectionName()).ToArray()));
            }
            return type.FullName;
        }

        internal static Instruction FixReturns(this ILProcessor ilProcessor)
        {
            var methodDefinition = ilProcessor.Body.Method;

            if (methodDefinition.ReturnType == methodDefinition.Module.TypeSystem.Void)
            {
                var instructions = ilProcessor.Body.Instructions.ToArray();

                var newReturnInstruction = ilProcessor.Create(OpCodes.Ret);
                ilProcessor.Append(newReturnInstruction);

                foreach (var instruction in instructions)
                {
                    if (instruction.OpCode == OpCodes.Ret)
                    {
                        var leaveInstruction = ilProcessor.Create(OpCodes.Leave, newReturnInstruction);
                        ilProcessor.Replace(instruction, leaveInstruction);
                        ilProcessor.ReplaceInstructionReferences(instruction, leaveInstruction);
                    }
                }

                return newReturnInstruction;
            }
            else
            {
                var instructions = ilProcessor.Body.Instructions.ToArray();

                var returnVariable = new VariableDefinition(methodDefinition.ReturnType);
                ilProcessor.Body.Variables.Add(returnVariable);

                var loadResultInstruction = ilProcessor.Create(OpCodes.Ldloc, returnVariable);
                ilProcessor.Append(loadResultInstruction);
                var newReturnInstruction = ilProcessor.Create(OpCodes.Ret);
                ilProcessor.Append(newReturnInstruction);

                foreach (var instruction in instructions)
                {
                    if (instruction.OpCode == OpCodes.Ret)
                    {
                        var leaveInstruction = ilProcessor.Create(OpCodes.Leave, loadResultInstruction);
                        ilProcessor.Replace(instruction, leaveInstruction);
                        ilProcessor.ReplaceInstructionReferences(instruction, leaveInstruction);
                        var saveResultInstruction = ilProcessor.Create(OpCodes.Stloc, returnVariable);
                        ilProcessor.InsertBefore(leaveInstruction, saveResultInstruction);
                        ilProcessor.ReplaceInstructionReferences(leaveInstruction, saveResultInstruction);
                    }
                }

                return loadResultInstruction;
            }
        }

        internal static void ReplaceInstructionReferences(
           this ILProcessor ilProcessor,
           Instruction oldInstruction,
           Instruction newInstruction)
        {
            foreach (var handler in ilProcessor.Body.ExceptionHandlers)
            {
                if (handler.FilterStart == oldInstruction)
                    handler.FilterStart = newInstruction;

                if (handler.TryStart == oldInstruction)
                    handler.TryStart = newInstruction;

                if (handler.TryEnd == oldInstruction)
                    handler.TryEnd = newInstruction;

                if (handler.HandlerStart == oldInstruction)
                    handler.HandlerStart = newInstruction;

                if (handler.HandlerEnd == oldInstruction)
                    handler.HandlerEnd = newInstruction;
            }

            // Update instructions with a target instruction
            foreach (var iteratedInstruction in ilProcessor.Body.Instructions)
            {
                var operand = iteratedInstruction.Operand;

                if (operand == oldInstruction)
                {
                    iteratedInstruction.Operand = newInstruction;
                    continue;
                }
                else if (operand is Instruction[])
                {
                    Instruction[] operands = (Instruction[])operand;
                    for (var i = 0; i < operands.Length; ++i)
                    {
                        if (operands[i] == oldInstruction)
                            operands[i] = newInstruction;
                    }
                }
            }
        }

        private static Instruction FirstInstructionSkipCtor(MethodDefinition Method)
        {
            var body = Method.Body;
            if (Method.IsConstructor && !Method.IsStatic)
            {
                return body.Instructions[1];
            }
            return body.Instructions[0];
        }

        private static void InjectTryFinally(MethodDefinition method, ModuleDefinition module, string type = "")
        {
            if (method.Body == null) return;
            if (method.Body.Instructions.Count == 0) return;
            if (method.IsConstructor && !method.IsStatic && method.Body.Instructions.Count == 1) return;

            var il = method.Body.GetILProcessor();
            var firstInstruction = FirstInstructionSkipCtor(method);

            var beginSample = il.Create(
                OpCodes.Call,
                module.ImportReference(m_beginSampleMethod));
            il.InsertBefore(il.Body.Instructions[0], beginSample);
            il.InsertBefore(il.Body.Instructions[0], il.Create(OpCodes.Ldstr, "[" + type + "C#]:" + method.DeclaringType.Name + "::" + method.Name));
            il.InsertBefore(il.Body.Instructions[0], il.Create(OpCodes.Nop));

            var returnInstruction = il.FixReturns();
            var beforeReturn = Instruction.Create(OpCodes.Nop);
            il.InsertBefore(returnInstruction, beforeReturn);

            var endSample = il.Create(
                OpCodes.Call,
                module.ImportReference(m_endSampleMethod));
            il.InsertBefore(returnInstruction, endSample);
            il.InsertBefore(returnInstruction, Instruction.Create(OpCodes.Endfinally));

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = firstInstruction,
                TryEnd = beforeReturn,
                HandlerStart = beforeReturn,
                HandlerEnd = returnInstruction,
            };

            method.Body.ExceptionHandlers.Add(handler);
            method.Body.InitLocals = true;
            method = method.Resolve();
        }
        #endregion

        #region tool
        private static SequencePoint GetSequencePoint(MethodBody body)
        {
            if (body == null)
            {
                return null;
            }
            Instruction instruction = body.Instructions.FirstOrDefault(x => x.SequencePoint != null);
            return instruction == null ? null : instruction.SequencePoint;
        }

        public static AssemblyDefinition LoadAssembly(string path)
        {
            AssemblyDefinition result = null;
            result = AssemblyDefinition.ReadAssembly(path);
            AddResolver(result);
            return result;
        }

        public static void WriteAssembly(string path, AssemblyDefinition ass)
        {
            ass.Write(path);
        }

    
        public static void Recompile(string label, bool flag)
        {
            BuildTargetGroup bg = BuildTargetGroup.Standalone;
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    bg = BuildTargetGroup.Android;
                    break;
                case BuildTarget.iOS:
                    bg = BuildTargetGroup.iOS;
                    break;
            }
            string path = PlayerSettings.GetScriptingDefineSymbolsForGroup(bg);

            if (flag == path.Contains(label))
                return;

            if (flag)
            {
                path += ";" + label;
            }
            else
            {
                string[] heads = path.Split(';');
                path = "";

                foreach (var item in heads)
                {
                    if (item == label)
                    {
                        continue;
                    }
                    path += item + ";";
                }
            }
            PlayerSettings.SetScriptingDefineSymbolsForGroup(bg, path);
        }

        private static void AddResolver(AssemblyDefinition assembly)
        {
            var assemblyResolver = assembly.MainModule.AssemblyResolver as DefaultAssemblyResolver;
            HashSet<string> paths = new HashSet<string>();
            paths.Add("./Library/ScriptAssemblies/");
            foreach (string path in (from asm in System.AppDomain.CurrentDomain.GetAssemblies()
                                     select asm.ManifestModule.FullyQualifiedName).Distinct<string>())
            {
                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!paths.Contains(dir))
                    {
                        paths.Add(dir);
                    }
                }
                catch
                {
                }
            }

            foreach (var item in paths)
            {
                assemblyResolver.AddSearchDirectory(item);
            }
        }
        #endregion
        
    }
}