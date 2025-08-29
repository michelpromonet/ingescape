﻿/*  =========================================================================
Ingescape.cs

Copyright (c) the Contributors as noted in the AUTHORS file.
This file is part of Ingescape, see https://github.com/zeromq/ingescape.

This Source Code Form is subject to the terms of the Mozilla Public
License, v. 2.0. If a copy of the MPL was not distributed with this
file, You can obtain one at http://mozilla.org/MPL/2.0/.
=========================================================================
*/


using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Ingescape
{
    public partial class Igs
    {
        #region Path to library C IngeScape
#if RELEASE
        internal const string ingescapeDLLPath = "ingescape";
#elif DEBUG
        internal const string ingescapeDLLPath = "ingescaped";
#endif
        #endregion

        #region Agent initialization, control and events

            #region start & stop ingescape
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_start_with_device(IntPtr device, uint port);
        public static Result StartWithDevice(string networkDevice, uint port)
        {
            // ingescape provide devices in Latin-1, need to start with Latin-1
            Result result = igs_start_with_device(StringToLatin1Ptr(networkDevice), port);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_start_with_ip(IntPtr ipAddress, uint port);
        public static Result StartWithIp(string ipAddress, uint port)
        {
            IntPtr ipAsPtr = StringToUTF8Ptr(ipAddress);
            Result res = igs_start_with_ip(ipAsPtr, port);
            Marshal.FreeHGlobal(ipAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_stop();
        /// <summary>
        /// <para>Ingescape can be stopped either from the applications itself
        /// or from the network.When ingescape is stopped from the network,
        /// the application can be notified and take actions such as stopping, entering a specific mode, etc. </para>
        /// 
        /// <para>To stop ingescape from its hosting application,
        /// just call igs_stop().</para>
        /// 
        /// <para>To be notified that Ingescape has been stopped, you can:<br />
        /// - register a callabck with igs_observe_forced_stop.<br />
        /// WARNING: this callback will be executed from the ingescape thread with potential thread-safety issues depending on your application structure.<br />
        /// - periodically check the value returned by igs_is_started()<br />
        /// In any case, igs_stop() MUST NEVER BE CALLED directly from any Ingescape callback, because it would create a deadlock betweenthe main thread and the ingescape thread. </para>
        /// </summary>
        public static void Stop() { igs_stop(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_is_started();
        public static bool IsStarted() { return igs_is_started(); }

        private static ForcedStopFunctionC _OnForcedStopCallback;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ForcedStopFunctionC(IntPtr myData);
        public delegate void ForcedStopFunction(object myData);

        static void OnForcedStopCallback(IntPtr myData)
        {
            Tuple<ForcedStopFunction, object> tupleData = (Tuple<ForcedStopFunction, object>)GCHandle.FromIntPtr(myData).Target;
            ForcedStopFunction cSharpFunction = tupleData.Item1;
            object data = tupleData.Item2;
            cSharpFunction(data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_forced_stop([MarshalAs(UnmanagedType.FunctionPtr)] ForcedStopFunctionC cb, IntPtr myData);
        /// <summary>
        /// register a callback when the agent is forced to Stop by the ingescape platform. <br />
        /// WARNING: this callback will be executed from the ingescape thread with potential thread-safety issues depending on your application structure.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="myData"></param>
        public static void ObserveForcedStop(ForcedStopFunction callback, object myData)
        {
            Tuple<ForcedStopFunction, object> tupleData = new Tuple<ForcedStopFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);
            if (_OnForcedStopCallback == null)
                _OnForcedStopCallback = OnForcedStopCallback;
            
            igs_observe_forced_stop(_OnForcedStopCallback, data);
        }
        #endregion

            #region agent name

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_agent_set_name(IntPtr name);
        public static void AgentSetName(string name)
        {
            IntPtr strPtr = StringToUTF8Ptr(name);
            igs_agent_set_name(strPtr);
            Marshal.FreeHGlobal(strPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_agent_name();
        public static string AgentName()
        {
            IntPtr ptr = igs_agent_name();
            return PtrToStringFromUTF8(ptr);
        }
        #endregion

            #region agent uuid
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_agent_uuid();
        public static string AgentUUID()
        {
            IntPtr ptr = igs_agent_uuid();
            return PtrToStringFromUTF8(ptr);
        }
        #endregion

            #region control agent state
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_agent_set_state(IntPtr state);
        public static void AgentSetState(string state)
        {
            IntPtr stateAsPtr = StringToUTF8Ptr(state);
            igs_agent_set_state(stateAsPtr);
            Marshal.FreeHGlobal(stateAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_agent_state();
        public static string AgentState()
        {
            IntPtr ptr = igs_agent_state();
            return PtrToStringFromUTF8(ptr);
        }
        #endregion

            #region mute the agent
        private static MuteFunctionC _OnMutedCallback;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MuteFunctionC(bool AgentIsMuted, IntPtr myData);
        public delegate void MuteFunction(bool AgentIsMuted, object myData);

        static void OnMutedCallback(bool isMuted, IntPtr myData)
        {
            Tuple<MuteFunction, object> tupleData = (Tuple<MuteFunction, object>)GCHandle.FromIntPtr(myData).Target;
            MuteFunction cSharpFunction = tupleData.Item1;
            object data = tupleData.Item2;
            cSharpFunction(isMuted, data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_agent_mute();
        public static int AgentMute() { return igs_agent_mute(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_agent_unmute();
        public static int AgentUnmute() { return igs_agent_unmute(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_agent_is_muted();
        public static bool AgentIsMuted() { return igs_agent_is_muted(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_mute([MarshalAs(UnmanagedType.FunctionPtr)] MuteFunctionC cb, IntPtr myData);
        public static void ObserveMute(MuteFunction callback, object myData)
        {
            Tuple<MuteFunction, object> tupleData = new Tuple<MuteFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);
            if (_OnMutedCallback == null)
                _OnMutedCallback = OnMutedCallback;
            
            igs_observe_mute(_OnMutedCallback, data);
        }
        #endregion

            #region freeze the agent
        private static FreezeFunctionC _OnFreezeCallback;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FreezeFunctionC(bool isPaused, IntPtr myData);
        public delegate void FreezeFunction(bool isPaused, object myData);

        static void OnFreezeCallback(bool isFreeze, IntPtr myData)
        {
            Tuple<FreezeFunction, object> tupleData = (Tuple<FreezeFunction, object>)GCHandle.FromIntPtr(myData).Target;
            FreezeFunction cSharpFunction = tupleData.Item1;
            object data = tupleData.Item2;
            cSharpFunction(isFreeze, data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_freeze();

        /// <summary>
        /// When freezed, agent will not send anything on its outputs and
        /// its inputs are not reactive to external data.<br />
        /// NB: the internal semantics of freeze and unfreeze for a given agent
        /// are up to the developer and can be controlled using callbacks and igs_observe_freeze
        /// </summary>
        /// <returns></returns>
        public static Result Freeze() { return igs_freeze(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_is_frozen();
        public static bool IsFrozen() { return igs_is_frozen(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_unfreeze();

        /// <summary>
        /// When freezed, agent will not send anything on its outputs and
        /// its inputs are not reactive to external data.<br />
        /// NB: the internal semantics of freeze and unfreeze for a given agent
        /// are up to the developer and can be controlled using callbacks and igs_observe_freeze
        /// </summary>
        /// <returns></returns>
        public static int Unfreeze() { return igs_unfreeze(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_observe_freeze(FreezeFunctionC cb, IntPtr myData);
        public static int ObserveFreeze(FreezeFunction callback, object myData)
        {
            Tuple<FreezeFunction, object> tupleData = new Tuple<FreezeFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);
            if (_OnFreezeCallback == null)
                _OnFreezeCallback = OnFreezeCallback;
            
            return igs_observe_freeze(_OnFreezeCallback, data);
        }
        #endregion

            #region observe agents
        private static AgentEventsFunctionC _OnAgentEvents;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AgentEventsFunctionC(AgentEvent agentEvent, IntPtr uuid, IntPtr name, IntPtr eventData, IntPtr myData);
        public delegate void AgentEventsFunction(AgentEvent agentEvent,
                                   string uuid,
                                   string name,
                                   object eventData,
                                   object myData);

        static void OnAgentEventsCallBack(AgentEvent agentEvent, IntPtr uuid, IntPtr name, IntPtr eventData, IntPtr myData)
        {
            GCHandle gCHandleData = GCHandle.FromIntPtr(myData);
            object eventDataAsObject = null;
            if (eventData != IntPtr.Zero && agentEvent != AgentEvent.PeerEntered)
                eventDataAsObject = PtrToStringFromUTF8(eventData);

            Tuple<AgentEventsFunction, object> tuple = (Tuple<AgentEventsFunction, object>)gCHandleData.Target;
            object data = tuple.Item2;

            AgentEventsFunction cSharpFunction = tuple.Item1;
            cSharpFunction(agentEvent, PtrToStringFromUTF8(uuid), PtrToStringFromUTF8(name), eventDataAsObject, data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_observe_agent_events(AgentEventsFunctionC cb, IntPtr myData);
        public static int ObserveAgentEvents(AgentEventsFunction callback, object myData)
        {
            Tuple<AgentEventsFunction, object> tupleData = new Tuple<AgentEventsFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);
            if (_OnAgentEvents == null)
                _OnAgentEvents = OnAgentEventsCallBack;
            
            return igs_observe_agent_events(_OnAgentEvents, data);
        }
        #endregion

        #endregion

        #region Edit & inspect agent definition (inputs, outputs, services, attributes)s

            #region Package, class, description, version

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_definition_set_package(IntPtr package);

        /// <summary>
        /// <code>
        /// In a Model-Based System Engineering (MBSE) context, an agent may
        /// provide additonal information regarding its category and role in
        /// a given system.These information are represented by:
        /// • A package, positioning the class inside a larger set,
        /// • A class, naming the agent in the context of a given system,
        /// • A free-text description for the role and activities of the agent,
        /// • A version.
        /// The class is set by default to the name of the agent.
        /// The package generally complies with a hierarchical structure using '::'
        /// as a separator, e.g.level1::level2::level3.Note that the library does
        /// not make any verification.
        /// </code>
        /// </summary>
        /// <param name="package"></param>
        public static void DefinitionSetPackage(string package)
        {
            IntPtr packageAsPtr = StringToUTF8Ptr(package);
            igs_definition_set_package(packageAsPtr);
            Marshal.FreeHGlobal(packageAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_definition_package(); //caller owns returned value

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static string DefinitionPackage(){ return PtrToStringFromUTF8(igs_definition_package()); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_definition_set_class(IntPtr my_class);

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static void DefinitionSetClass(string myClass)
        {
            IntPtr classAsPtr = StringToUTF8Ptr(myClass);
            igs_definition_set_class(classAsPtr);
            Marshal.FreeHGlobal(classAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_definition_class(); //caller owns returned value

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static string DefinitionClass() { return PtrToStringFromUTF8(igs_definition_class()); }


        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_definition_description();

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static string DefinitionDescription()
        {
            IntPtr ptr = igs_definition_description();
            return PtrToStringFromUTF8(ptr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_definition_version();

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static string DefinitionVersion()
        {
            IntPtr ptr = igs_definition_version();
            return (ptr == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_definition_set_description(IntPtr description);

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static void DefinitionSetDescription(string description)
        {
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            igs_definition_set_description(descriptionAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_definition_set_version(IntPtr Version);

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPackage"/>
        /// </summary>
        public static void DefinitionSetVersion(string Version)
        {
            IntPtr versionAsPtr = StringToUTF8Ptr(Version);
            igs_definition_set_version(versionAsPtr);
            Marshal.FreeHGlobal(versionAsPtr);
        }
        #endregion

            #region create & remove inputs/outputs

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_create(IntPtr name, IopValueType value_type, IntPtr value, uint size);
        public static Result InputCreate(string name, IopValueType type, object value = null)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            if (value != null)
            {
                uint size;
                IntPtr valuePtr;
                if (value.GetType() == typeof(string))
                    valuePtr = StringToUTF8Ptr(Convert.ToString(value), out size);
                else if (value.GetType() == typeof(bool))
                    valuePtr = BoolToPtr(Convert.ToBoolean(value), out size);
                else if (value.GetType() == typeof(byte[]))
                    valuePtr = DataToPtr((byte[])value, out size);
                else if (value.GetType() == typeof(double))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(float))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(int))
                    valuePtr = IntToPtr(Convert.ToInt32(value), out size);
                else 
                    return Result.Failure;

                Result res = igs_input_create(nameAsPtr, type, valuePtr, size);
                Marshal.FreeHGlobal(nameAsPtr);
                Marshal.FreeHGlobal(valuePtr);
                return res;
            }
            else
            {
                Result res = igs_input_create(nameAsPtr, type, IntPtr.Zero, 0);
                Marshal.FreeHGlobal(nameAsPtr);
                return res;
            }
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_create(IntPtr name, IopValueType type, IntPtr value, uint size);
        public static Result OutputCreate(string name, IopValueType type, object value = null)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            if (value != null)
            {
                uint size;
                IntPtr valuePtr;
                if (value.GetType() == typeof(string))
                    valuePtr = StringToUTF8Ptr(Convert.ToString(value), out size);
                else if (value.GetType() == typeof(bool))
                    valuePtr = BoolToPtr(Convert.ToBoolean(value), out size);
                else if (value.GetType() == typeof(byte[]))
                    valuePtr = DataToPtr((byte[])value, out size);
                else if (value.GetType() == typeof(double))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(float))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(int))
                    valuePtr = IntToPtr(Convert.ToInt32(value), out size);
                else
                    return Result.Failure;

                Result res = igs_output_create(nameAsPtr, type, valuePtr, size);
                Marshal.FreeHGlobal(nameAsPtr);
                Marshal.FreeHGlobal(valuePtr);
                return res;
            }
            else
            {
                Result res = igs_output_create(nameAsPtr, type, IntPtr.Zero, 0);
                Marshal.FreeHGlobal(nameAsPtr);
                return res;
            }
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_remove(IntPtr name);
        public static Result InputRemove(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result res = igs_input_remove(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_remove(IntPtr name);
        public static Result OutputRemove(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result res = igs_output_remove(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }
        #endregion

            #region inputs/outputs type, list and existence

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IopValueType igs_input_type(IntPtr name);
        public static IopValueType InputType(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IopValueType type = igs_input_type(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return type;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IopValueType igs_output_type(IntPtr name);
        public static IopValueType OutputType(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IopValueType type = igs_output_type(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return type;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_input_count();
        public static int InputCount() { return igs_input_count(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_output_count();
        public static int OutputCount() { return igs_output_count(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_input_list(ref int nbOfElements);
        public static string[] InputList()
        {
            int nbOfElements = 0;
            string[] list = null;
            IntPtr intptr = igs_input_list(ref nbOfElements);
            if (intptr != IntPtr.Zero)
            {
                IntPtr[] intPtrArray = new IntPtr[nbOfElements];
                list = new string[nbOfElements];
                Marshal.Copy(intptr, intPtrArray, 0, nbOfElements);
                for (int i = 0; i < nbOfElements; i++)
                    list[i] = Marshal.PtrToStringAnsi(intPtrArray[i]);
                Igs.igs_free_io_list(intptr, nbOfElements);
            }
            return list;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_output_list(ref int nbOfElements);
        public static string[] OutputList()
        {
            int nbOfElements = 0;
            string[] list = null;
            IntPtr intptr = igs_output_list(ref nbOfElements);
            if (intptr != IntPtr.Zero)
            {
                IntPtr[] intPtrArray = new IntPtr[nbOfElements];
                list = new string[nbOfElements];
                Marshal.Copy(intptr, intPtrArray, 0, nbOfElements);
                for (int i = 0; i < nbOfElements; i++)
                    list[i] = Marshal.PtrToStringAnsi(intPtrArray[i]);
                Igs.igs_free_io_list(intptr, nbOfElements);
            }
            return list;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_free_io_list(IntPtr list, int nbOfElements);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_input_exists(IntPtr name);
        public static bool InputExists(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_input_exists(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_output_exists(IntPtr name);
        public static bool OutputExists(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_output_exists(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }
        #endregion
 
            #region load / set / get / clear definition

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_definition_load_str(IntPtr json_str);
        public static Result DefinitionLoadStr(string json)
        {
            IntPtr jsonAsPtr = StringToUTF8Ptr(json);
            Result res = igs_definition_load_str(jsonAsPtr);
            Marshal.FreeHGlobal(jsonAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_definition_load_file(IntPtr file_path);
        public static Result DefinitionLoadFile(string file_path)
        {
            IntPtr pathAsPtr = StringToUTF8Ptr(file_path);
            Result res = igs_definition_load_file(pathAsPtr);
            Marshal.FreeHGlobal(pathAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_clear_definition();

        /// <summary>
        /// clears definition data for the agent
        /// </summary>
        /// <returns></returns>
        public static int ClearDefinition() { return igs_clear_definition(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_definition_json();

        /// <summary>
        /// returns json string
        /// </summary>
        /// <returns></returns>
        public static string DefinitionJson()
        {
            IntPtr ptr = igs_definition_json();
            return (ptr == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }
        #endregion

            #region read IOs per value type

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_input_bool(IntPtr name);
        public static bool InputBool(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_input_bool(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_input_int(IntPtr name);
        public static int InputInt(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            int value = igs_input_int(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern double igs_input_double(IntPtr name);
        public static double InputDouble(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            double value = igs_input_double(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_input_string(IntPtr name);
        public static string InputString(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = igs_input_string(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            string value = PtrToStringFromUTF8(valueAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_data(IntPtr name, ref IntPtr data, ref uint size);
        public static byte[] InputData(string name)
        {
            uint size = 0;
            byte[] data = null;
            IntPtr ptr = IntPtr.Zero;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result result = igs_input_data(nameAsPtr, ref ptr, ref size);
            Marshal.FreeHGlobal(nameAsPtr);
            if (result == Result.Success)
            {
                data = new byte[size];
                if (ptr != IntPtr.Zero)
                    Marshal.Copy(ptr, data, 0, (int)size);
            }
            return data;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_output_bool(IntPtr name);
        public static bool OutputBool(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_output_bool(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_output_int(IntPtr name);
        public static int OutputInt(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            int value = igs_output_int(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern double igs_output_double(IntPtr name);
        public static double OutputDouble(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            double value = igs_output_double(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_output_string(IntPtr name);
        public static string OutputString(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = igs_output_string(nameAsPtr);
            string value = PtrToStringFromUTF8(valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_data(IntPtr name, ref IntPtr data, ref uint size);
        public static byte[] OutputData(string name)
        {
            uint size = 0;
            byte[] data = null;
            IntPtr ptr = IntPtr.Zero;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result result = igs_output_data(nameAsPtr, ref ptr, ref size);
            Marshal.FreeHGlobal(nameAsPtr);
            if (result == Result.Success)
            {
                data = new byte[size];
                if (ptr != IntPtr.Zero)
                    Marshal.Copy(ptr, data, 0, (int)size);
            }
            return data;
        }
        #endregion

            #region write IOs per value type
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_bool(IntPtr name, bool value);
        public static Result InputSetBool(string name, bool value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_input_set_bool(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_int(IntPtr name, int value);
        public static Result InputSetInt(string name, int value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_input_set_int(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_double(IntPtr name, double value);
        public static Result InputSetDouble(string name, double value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_input_set_double(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_string(IntPtr name, IntPtr value);
        public static Result InputSetString(string name, string value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = StringToUTF8Ptr(value);
            result = igs_input_set_string(nameAsPtr, valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_impulsion(IntPtr name);
        public static Result InputSetImpulsion(string name)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_input_set_impulsion(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_data(IntPtr name, IntPtr value, uint size);
        public static Result InputSetData(string name, byte[] value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            uint size = Convert.ToUInt32(((byte[])value).Length);
            IntPtr valueAsPtr = Marshal.AllocHGlobal((int)size);
            Marshal.Copy(value, 0, valueAsPtr, (int)size);
            result = igs_input_set_data(nameAsPtr, valueAsPtr, size);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_bool(IntPtr name, bool value);
        public static Result OutputSetBool(string name, bool value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_output_set_bool(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_int(IntPtr name, int value);
        public static Result OutputSetInt(string name, int value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_output_set_int(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_double(IntPtr name, double value);
        public static Result OutputSetDouble(string name, double value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_output_set_double(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_string(IntPtr name, IntPtr value);
        public static Result OutputSetString(string name, string value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = StringToUTF8Ptr(value);
            result = igs_output_set_string(nameAsPtr, valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_impulsion(IntPtr name);
        public static Result OutputSetImpulsion(string name)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_output_set_impulsion(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_data(IntPtr name, IntPtr value, uint size);
        public static Result OutputSetData(string name, byte[] value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            uint size = Convert.ToUInt32(((byte[])value).Length);
            IntPtr valueAsPtr = Marshal.AllocHGlobal((int)size);
            Marshal.Copy(value, 0, valueAsPtr, (int)size);
            result = igs_output_set_data(nameAsPtr, valueAsPtr, size);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }
        #endregion

            #region  Constraints on IOs

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_constraints_enforce(bool enforce);//default is false, i.e. disabled

        /// <summary>
        /// <inheritdoc cref="InputAddConstraint"/>
        /// </summary>
        /// <param name="enforce"></param>
        public static void ConstraintsEnforce(bool enforce){ igs_constraints_enforce(enforce); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_add_constraint(IntPtr name, IntPtr constraint);

        /// <summary>
        /// <para>
        /// Constraints enable verifications upon sending or receiving information
        /// with inputs and outputs.<br />
        /// The syntax for the constraints is global but
        /// some constraints only apply to certain types:</para>
        /// <code xml:space="preserve">
        /// Integers and doubles:<br />
        ///     "max 10.123"  : applies a max allowed value on the IO<br />
        ///     "min -10" : applies a min allowed value on the IO<br />
        ///     "[-10, .1]" : applies min and max allowed values on the IO<br />
        /// Strings:<br />
        ///     "~ regular_expression", e.g. "~ \\d+(\.\\d+)?)":<br />
        ///     IOs of type STRING must match the regular expression<br />
        /// Regular expressions are based on CZMQ integration of SLRE with the<br />
        /// following syntax:<br />
        ///     ^               Match beginning of a buffer
        ///     $               Match end of a buffer
        ///     ()              Grouping and substring capturing
        ///     [...]           Match any character from set, caution: range-based syntax such as [0..9] is NOT supported
        ///     [^...]          Match any character but ones from set
        ///     \s              Match whitespace
        ///     \S              Match non-whitespace
        ///     \d              Match decimal digit
        ///     \r              Match carriage return
        ///     \n              Match newline
        ///     +               Match one or more times (greedy)
        ///     +?              Match one or more times (non-greedy)
        ///     *               Match zero or more times(greedy)
        ///     *?              Match zero or more times(non-greedy)
        ///     ?               Match zero or once
        ///     \xDD            Match byte with hex value 0xDD
        ///     \meta           Match one of the meta character: ^$().[*+?\
        /// </code>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="constraint"></param>
        /// <returns></returns>
        public static Result InputAddConstraint(string name, string constraint)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr constraintAsPtr = StringToUTF8Ptr(constraint);
            result = igs_input_add_constraint(nameAsPtr, constraintAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(constraintAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_add_constraint(IntPtr name, IntPtr constraint);

        /// <summary>
        /// <inheritdoc cref="InputAddConstraint"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="constraint"></param>
        /// <returns></returns>
        public static Result OutputAddConstraint(string name, string constraint)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr constraintAsPtr = StringToUTF8Ptr(constraint);
            result = igs_output_add_constraint(nameAsPtr, constraintAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(constraintAsPtr);
            return result;
        }
        #endregion  

            #region IO description

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_description(IntPtr name, IntPtr description);
        public static Result InputSetDescription(string name, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_input_set_description(nameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_input_description(IntPtr name);
        public static string InputDescription(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = igs_input_description(nameAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_description(IntPtr name, IntPtr description);
        public static Result OutputSetDescription(string name, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_output_set_description(nameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_output_description(IntPtr name);
        public static string OutputDescription(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = igs_output_description(nameAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }
        #endregion

            #region IO detailed type
        
        [DllImport(Igs.ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_input_set_detailed_type(IntPtr paramName, IntPtr typeName, IntPtr specification);

        /// <summary>
        /// decribe precise specifications for IOs,
        /// around a detailed type.Specifications are descriptive only. <br />
        /// Ingescape does not check anything that is passed here.<br />
        /// For example, the detailed type can be 'protobuf' and the specification
        /// can be an actual protobuf structure in proto format.<br />
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="typeName"></param>
        /// <param name="specification"></param>
        /// <returns></returns>
        public static Result InputSetDetailedType(string paramName, string typeName, string specification)
        {
            IntPtr paramNameAsPtr = Igs.StringToUTF8Ptr(paramName);
            IntPtr typeAsPtr = Igs.StringToUTF8Ptr(typeName);
            IntPtr specificationAsPtr = Igs.StringToUTF8Ptr(specification);
            Result res = igs_input_set_detailed_type(paramNameAsPtr, typeAsPtr, specificationAsPtr);
            Marshal.FreeHGlobal(paramNameAsPtr);
            Marshal.FreeHGlobal(typeAsPtr);
            Marshal.FreeHGlobal(specificationAsPtr);
            return res;
        }

        [DllImport(Igs.ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_output_set_detailed_type(IntPtr paramName, IntPtr typeName, IntPtr specification);

        /// <summary>
        /// <inheritdoc cref="InputSetDetailedType"/>
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="typeName"></param>
        /// <param name="specification"></param>
        /// <returns></returns>
        public static Result OutputSetDetailedType(string paramName, string typeName, string specification)
        {
            IntPtr paramNameAsPtr = Igs.StringToUTF8Ptr(paramName);
            IntPtr typeAsPtr = Igs.StringToUTF8Ptr(typeName);
            IntPtr specificationAsPtr = Igs.StringToUTF8Ptr(specification);
            Result res = igs_output_set_detailed_type(paramNameAsPtr, typeAsPtr, specificationAsPtr);
            Marshal.FreeHGlobal(paramNameAsPtr);
            Marshal.FreeHGlobal(typeAsPtr);
            Marshal.FreeHGlobal(specificationAsPtr);
            return res;
        }

        #endregion

            #region Clear IO 

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_input(IntPtr name);

        /// <summary>
        /// Clear IO data in memory without having to write an empty value
        /// into the IO.Especially useful for IOs handling large strings and data.
        /// </summary>
        /// <param name="name"></param>
        public static void ClearInput(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            igs_clear_input(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_output(IntPtr name);
        public static void ClearOutput(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            igs_clear_output(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
        }
        #endregion

            #region observe changes to an IO

        private static IopFunctionC _OnIOPCallback;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void IopFunctionC(IopType iopType,
                                            IntPtr name,
                                            IopValueType valueType,
                                            IntPtr value,
                                            uint valueSize,
                                            IntPtr myData);
        public delegate void IopFunction(IopType iopType, string name, IopValueType valueType, object value, object myData);

        static void OnIOPCallback(IopType iopType,
                                IntPtr name, IopValueType valueType,
                                IntPtr value,
                                uint valueSize,
                                IntPtr myData)
        {
            GCHandle gCHandleData = GCHandle.FromIntPtr(myData);
            Tuple<IopFunction, object> tuple = (Tuple<IopFunction, object>)gCHandleData.Target;
            object data = tuple.Item2;
            IopFunction cSharpFunction = tuple.Item1;
            object newValue = null;
            switch (valueType)
            {
                case IopValueType.Bool:
                    newValue = PtrToBool(value);
                    break;
                case IopValueType.Data:
                    newValue = PtrToData(value, (int)valueSize);
                    break;
                case IopValueType.Double:
                    newValue = PtrToDouble(value);
                    break;
                case IopValueType.Impulsion:
                    break;
                case IopValueType.Integer:
                    newValue = PtrToInt(value);
                    break;
                case IopValueType.String:
                    newValue = PtrToStringFromUTF8(value);
                    break;
            }
            string decodedName = PtrToStringFromUTF8(name);
            cSharpFunction(iopType, decodedName, valueType, newValue, data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_input(IntPtr name,
            [MarshalAs(UnmanagedType.FunctionPtr)] IopFunctionC cb,
            IntPtr myData);
        public static void ObserveInput(string inputName, IopFunction callback, object myData)
        {
            Tuple<IopFunction, object> tupleData = new Tuple<IopFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);

            if (_OnIOPCallback == null)
                _OnIOPCallback = OnIOPCallback;
            
            IntPtr nameAsPtr = StringToUTF8Ptr(inputName);
            igs_observe_input(nameAsPtr, _OnIOPCallback, data);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_output(IntPtr name, IopFunctionC cb, IntPtr myData);
        public static void ObserveOutput(string outputName, IopFunction callback, object myData)
        {
            Tuple<IopFunction, object> tupleData = new Tuple<IopFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);

            if (_OnIOPCallback == null)
                _OnIOPCallback = OnIOPCallback;

            IntPtr nameAsPtr = StringToUTF8Ptr(outputName);
            igs_observe_output(nameAsPtr, _OnIOPCallback, data);
            Marshal.FreeHGlobal(nameAsPtr);
        }
        #endregion

            #region mute or unmute an output
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_output_mute(IntPtr name);
        public static void OutputMute(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            igs_output_mute(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_output_unmute(IntPtr name);
        public static void OutputUnmute(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            igs_output_unmute(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_output_is_muted(IntPtr name);
        public static bool OutputIsMuted(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_output_is_muted(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }
        #endregion

            #region Services edition & inspection

        //services arguments
        [StructLayout(LayoutKind.Explicit)]
        internal struct UnionServiceArgument
        {
            [FieldOffset(0)]
            public bool b;
            [FieldOffset(0)]
            public int i;
            [FieldOffset(0)]
            public double d;
            [FieldOffset(0)]
            public IntPtr c;
            [FieldOffset(0)]
            public IntPtr data;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StructServiceArgument
        {
            public IntPtr name;
            public IntPtr description;
            public IopValueType type;
            public UnionServiceArgument union;
            public uint size;
            public IntPtr next;
        }

        // Arguments management
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_service_args_add_int(ref IntPtr list, int value);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_service_args_add_bool(ref IntPtr list, bool value);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_service_args_add_double(ref IntPtr list, double value);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_service_args_add_string(ref IntPtr list, IntPtr value);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_service_args_add_data(ref IntPtr list, byte[] value, uint size);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_service_args_destroy(ref IntPtr list);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_args_clone(IntPtr list);
        private static List<ServiceArgument> ServiceArgsClone(List<ServiceArgument> list)
        {
            List<ServiceArgument> newServiceArguments = new List<ServiceArgument>(list);
            return newServiceArguments;
        }

        #region call a service hosted by another agent
        
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_call(IntPtr agentNameOrUUID,
                                               IntPtr serviceName,
                                               ref IntPtr list, IntPtr token);

        // For a null list
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_call(IntPtr agentNameOrUUID,
                                               IntPtr serviceName,
                                               IntPtr list, IntPtr token);

        /// <summary>
        /// Requires to pass an agent name or UUID, a service name and a list of arguments specific to the service.<br />
        /// Token is an optional information to help routing replies.<br />
        /// </summary>
        /// <param name="agentNameOrUUID"></param>
        /// <param name="serviceName"></param>
        /// <param name="arguments"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Result ServiceCall(string agentNameOrUUID, string serviceName, object[] arguments, string token = "")
        {
            IntPtr agentNameOrUUIDAsPtr = StringToUTF8Ptr(agentNameOrUUID);
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr tokenAsPtr = StringToUTF8Ptr(token);
            IntPtr ptr = IntPtr.Zero;
            Result success = Result.Failure;
            if (arguments != null)
            {
                int i = 0;
                foreach (object argument in arguments)
                {
                    i++;
                    if (argument != null)
                    {
                        if (argument.GetType() == typeof(bool))
                            igs_service_args_add_bool(ref ptr, Convert.ToBoolean(argument));
                        else if (argument.GetType() == typeof(byte[]))
                        {
                            byte[] data = (byte[])argument;
                            igs_service_args_add_data(ref ptr, data, (uint)data.Length);
                        }
                        else if (argument.GetType() == typeof(double) || argument.GetType() == typeof(float))
                            igs_service_args_add_double(ref ptr, Convert.ToDouble(argument));
                        else if (argument.GetType() == typeof(int))
                            igs_service_args_add_int(ref ptr, Convert.ToInt32(argument));
                        else if (argument.GetType() == typeof(string))
                        {
                            IntPtr argAsPtr = StringToUTF8Ptr(Convert.ToString(argument));
                            igs_service_args_add_string(ref ptr, argAsPtr);
                            Marshal.FreeHGlobal(argAsPtr);
                        }
                    }
                    else
                    {
                        Error(string.Format("argument at {0} is null. Cannot call service {1}", i.ToString(), serviceName));
                        igs_service_args_destroy(ref ptr);
                        return Result.Failure;
                    }
                }
                success = igs_service_call(agentNameOrUUIDAsPtr, serviceNameAsPtr, ref ptr, tokenAsPtr);
                igs_service_args_destroy(ref ptr);
            }
            else
            {
                success = igs_service_call(agentNameOrUUIDAsPtr, serviceNameAsPtr, ptr, tokenAsPtr);
                igs_service_args_destroy(ref ptr);
            }
            Marshal.FreeHGlobal(agentNameOrUUIDAsPtr);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(tokenAsPtr);
            return success;
        }
        #endregion

        #region create /remove / edit a service offered by our agent

        private static ServiceFunctionC _OnServiceCallback;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ServiceFunctionC(IntPtr senderAgentName,
                                            IntPtr senderAgentUUID,
                                            IntPtr serviceName,
                                            IntPtr firstArgument,
                                            uint nbArgs,
                                            IntPtr token,
                                            IntPtr myData);
        public delegate void ServiceFunction(string senderAgentName, string senderAgentUUID, string serviceName, List<ServiceArgument> arguments, string token, object myData);

        static void OnServiceCallback(IntPtr senderAgentName,
                                   IntPtr senderAgentUUID,
                                   IntPtr serviceName,
                                   IntPtr firstArgument,
                                   uint nbArgs,
                                   IntPtr token,
                                   IntPtr myData)
        {
            GCHandle gCHandle = GCHandle.FromIntPtr(myData);
            Tuple<ServiceFunction, object> tuple = (Tuple<ServiceFunction, object>)gCHandle.Target;
            object data = tuple.Item2;
            ServiceFunction cSharpFunction = tuple.Item1;
            string serviceNameAsString = PtrToStringFromUTF8(serviceName);
            List<ServiceArgument> serviceArguments = Igs.ServiceArgumentsListFromFirstArg(firstArgument);
            cSharpFunction(PtrToStringFromUTF8(senderAgentName), PtrToStringFromUTF8(senderAgentUUID), serviceNameAsString, serviceArguments, PtrToStringFromUTF8(token), data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_init(IntPtr name,
                                               [MarshalAs(UnmanagedType.FunctionPtr)] ServiceFunctionC cb,
                                               IntPtr myData);
        /// <summary>
        /// WARNING: only one callback shall be attached to a service
        /// (further attempts will be ignored and signaled by an error log).*/
        /// </summary>
        /// <param name="name"></param>
        /// <param name="callback"></param>
        /// <param name="myData"></param>
        /// <returns></returns>
        public static Result ServiceInit(string name, ServiceFunction callback, object myData)
        {
            Tuple<ServiceFunction, object> tupleData = new Tuple<ServiceFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);

            if (_OnServiceCallback == null)
                _OnServiceCallback = OnServiceCallback;
            
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result res = igs_service_init(nameAsPtr, _OnServiceCallback, data);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_remove(IntPtr name);
        public static Result ServiceRemove(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result res = igs_service_remove(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_set_description (IntPtr name, IntPtr description);
        public static Result ServiceSetDescription(string name, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_service_set_description(nameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_description (IntPtr name);
        public static string ServiceDescription(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = igs_service_description(nameAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_arg_add(IntPtr serviceName, IntPtr argName, IopValueType type);
        public static Result ServiceArgAdd(string serviceName, string argName, IopValueType type)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            Result res = igs_service_arg_add(nameAsPtr, argNameAsPtr, type);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(argNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_arg_remove(IntPtr serviceName, IntPtr argName);
        public static Result ServiceArgRemove(string serviceName, string argName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            Result res = igs_service_arg_remove(nameAsPtr, argNameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(argNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_arg_set_description(IntPtr serviceName, IntPtr argName, IntPtr description);
        public static Result ServiceArgSetDescription(string serviceName, string argName, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr argAsPtr = StringToUTF8Ptr(argName);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_service_arg_set_description(nameAsPtr, argAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(argAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_arg_description(IntPtr serviceName, IntPtr argName);
        public static string ServiceArgDescription(string serviceName, string argName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr argAsPtr = StringToUTF8Ptr(argName);
            IntPtr descriptionAsPtr = igs_service_arg_description(nameAsPtr, argAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(argAsPtr);
            return res;
        }
        #endregion

        #region replies are optional and used for specification purposes

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_reply_add(IntPtr serviceName, IntPtr replyName);
        public static Result ServiceReplyAdd(string serviceName, string replyName)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            Result res = igs_service_reply_add(serviceNameAsPtr, replyNameAsPtr);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_reply_remove(IntPtr serviceName, IntPtr replyName);
        public static Result ServiceReplyRemove(string serviceName, string replyName)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            Result res = igs_service_reply_remove(serviceNameAsPtr, replyNameAsPtr);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_reply_set_description (IntPtr serviceName, IntPtr replyName, IntPtr description);
        public static Result ServiceReplySetDescription(string serviceName, string replyName, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_service_reply_set_description(nameAsPtr, replyNameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_reply_description (IntPtr name, IntPtr replyName);
        public static string ServiceReplyDescription(string name, string replyName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr descriptionAsPtr = igs_service_reply_description(nameAsPtr, replyNameAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_reply_arg_add(IntPtr serviceName, IntPtr replyName, IntPtr argName, IopValueType type);
        public static Result ServiceReplyArgAdd(string serviceName, string replyName, string argName, IopValueType type)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            Result res = igs_service_reply_arg_add(serviceNameAsPtr, replyNameAsPtr, argNameAsPtr, type);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            Marshal.FreeHGlobal(argNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_reply_arg_set_description (IntPtr serviceName, IntPtr replyName, IntPtr argName, IntPtr description);
        public static Result ServiceReplyArgSetDescription(string serviceName, string replyName, string argName, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_service_reply_arg_set_description(nameAsPtr, replyNameAsPtr, argNameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            Marshal.FreeHGlobal(argNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_reply_arg_description (IntPtr name, IntPtr replyName, IntPtr argName);
        public static string ServiceReplyArgDescription(string name, string replyName, string argName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            IntPtr descriptionAsPtr = igs_service_reply_arg_description(nameAsPtr, replyNameAsPtr, argNameAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_service_reply_arg_remove(IntPtr serviceName, IntPtr replyName, IntPtr argName);
        public static Result ServiceReplyArgRemove(string serviceName, string replyName, string argName)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            Result res = igs_service_reply_arg_remove(serviceNameAsPtr, replyNameAsPtr, argNameAsPtr);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            Marshal.FreeHGlobal(argNameAsPtr);
            return res;
        }
        #endregion

        #region introspection for services, their arguments and optional replies

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igs_service_count();
        public static uint ServiceCount() { return igs_service_count(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_service_exists(IntPtr name);
        public static bool ServiceExists(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool res = igs_service_exists(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_list(ref uint nbOfElements);
        public static string[] ServiceList()
        {
            uint nbOfElements = 0;

            IntPtr intPtr = igs_service_list(ref nbOfElements);
            if (nbOfElements != 0)
            {
                IntPtr[] intPtrArray = new IntPtr[nbOfElements];
                Marshal.Copy(intPtr, intPtrArray, 0, (int)nbOfElements);
                string[] list = new string[nbOfElements];
                for (int i = 0; i < nbOfElements; i++)
                    list[i] = Marshal.PtrToStringAnsi(intPtrArray[i]);

                igs_free_services_list(intPtr, nbOfElements);
                return list;
            }
            else return null;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void igs_free_services_list(IntPtr list, uint numberOfServices);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_args_first(IntPtr serviceName);
        public static List<ServiceArgument> ServiceArgumentsList(string serviceName)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr ptrArgument = igs_service_args_first(serviceNameAsPtr);
            List<ServiceArgument> serviceArgumentsList = Igs.ServiceArgumentsListFromFirstArg(ptrArgument);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            return serviceArgumentsList;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igs_service_args_count(IntPtr name);
        public static uint ServiceArgsCount(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            uint res = igs_service_args_count(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_service_arg_exists(IntPtr serviceName, IntPtr argName);
        public static bool ServiceArgExists(string serviceName, string argName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr argAsPtr = StringToUTF8Ptr(argName);
            bool res = igs_service_arg_exists(nameAsPtr, argAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(argAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_service_has_replies(IntPtr serviceName);
        public static bool ServiceHasReplies(string serviceName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            bool res = igs_service_has_replies(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_service_has_reply(IntPtr serviceName, IntPtr replyName);
        public static bool ServiceHasReply(string serviceName, string replyName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            bool res = igs_service_has_reply(nameAsPtr, replyNameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_reply_names(IntPtr serviceName, ref uint serviceRepliesNbr);
        public static string[] ServiceReplyNames(string serviceName)
        {
            uint serviceRepliesNbr = 0;
            string[] replyNames = null;
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNamesAsPtr = igs_service_reply_names(serviceNameAsPtr, ref serviceRepliesNbr);
            if (serviceRepliesNbr != 0)
            {
                IntPtr[] replyNamesAsPtrArray = new IntPtr[serviceRepliesNbr];
                Marshal.Copy(replyNamesAsPtr, replyNamesAsPtrArray, 0, (int)serviceRepliesNbr);
                replyNames = new string[serviceRepliesNbr];
                for (int i = 0; i < serviceRepliesNbr; i++)
                    replyNames[i] = Marshal.PtrToStringAnsi(replyNamesAsPtrArray[i]);

                igs_free_services_list(replyNamesAsPtr, serviceRepliesNbr);
            }
            return replyNames;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_service_reply_args_first(IntPtr serviceName, IntPtr replyName);
        public static List<ServiceArgument> ServiceReplyArgumentsList(string serviceName, string replyName)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr ptrArgument = igs_service_reply_args_first(serviceNameAsPtr, replyNameAsPtr);
            List<ServiceArgument> serviceReplyArgumentsList = null;
            if (ptrArgument != IntPtr.Zero)
            {
                serviceReplyArgumentsList = new List<ServiceArgument>();
                while (ptrArgument != IntPtr.Zero)
                {
                    // Marshals data from an unmanaged block of memory to a newly allocated managed object of the type specified by a generic type parameter.
                    StructServiceArgument structArgument = Marshal.PtrToStructure<StructServiceArgument>(ptrArgument);

                    object value = null;

                    switch (structArgument.type)
                    {
                        case IopValueType.Bool:
                            value = structArgument.union.b;
                            break;

                        case IopValueType.Integer:
                            value = structArgument.union.i;
                            break;

                        case IopValueType.Double:
                            value = structArgument.union.d;
                            break;

                        case IopValueType.String:
                            value = PtrToStringFromUTF8(structArgument.union.c);
                            break;

                        case IopValueType.Data:
                            byte[] byteArray = new byte[structArgument.size];

                            // Copies data from an unmanaged memory pointer to a managed 8-bit unsigned integer array.
                            // Copy the content of the IntPtr to the byte array
                            if (structArgument.union.data != IntPtr.Zero)
                                Marshal.Copy(structArgument.union.data, byteArray, 0, (int)structArgument.size);
                            else
                                byteArray = null;

                            value = byteArray;
                            break;

                        default:
                            break;
                    }

                    ServiceArgument serviceArgument = new ServiceArgument(PtrToStringFromUTF8(structArgument.name), structArgument.type, value);
                    serviceReplyArgumentsList.Add(serviceArgument);
                    ptrArgument = structArgument.next;
                }
            }
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return serviceReplyArgumentsList;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igs_service_reply_args_count(IntPtr serviceName, IntPtr replyName);
        public static uint ServiceReplyArgsCount(string serviceName, string replyName)
        {
            IntPtr serviceNameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            uint res = igs_service_reply_args_count(serviceNameAsPtr, replyNameAsPtr);
            Marshal.FreeHGlobal(serviceNameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_service_reply_arg_exists(IntPtr serviceName, IntPtr replyName, IntPtr argName);
        public static bool ServiceReplyArgExists(string serviceName, string replyName, string argName)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(serviceName);
            IntPtr replyNameAsPtr = StringToUTF8Ptr(replyName);
            IntPtr argNameAsPtr = StringToUTF8Ptr(argName);
            bool res = igs_service_reply_arg_exists(nameAsPtr, replyNameAsPtr, argNameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(replyNameAsPtr);
            Marshal.FreeHGlobal(argNameAsPtr);
            return res;
        }
        #endregion

        #endregion

            #region Attributes edition & inspection

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_create(IntPtr name, IopValueType type, IntPtr value, uint size);

        /// <summary>
        /// Attributes are very similar to IOs, except that they are not exposed
        /// to other agents.They are not usable in mappings. <br />
        /// Attributes are used to expose internal variables into agents, which are
        /// included in their definition.Attributes generally describe key variables
        /// in an agent, which affect the internal behavior of the agent.<br />
        /// NOTE: Attributes used to be named parameters in older versions.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result AttributeCreate(string name, IopValueType type, object value = null)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            if (value != null)
            {
                uint size;
                IntPtr valuePtr;
                if (value.GetType() == typeof(string))
                    valuePtr = StringToUTF8Ptr(Convert.ToString(value), out size);
                else if (value.GetType() == typeof(bool))
                    valuePtr = BoolToPtr(Convert.ToBoolean(value), out size);
                else if (value.GetType() == typeof(byte[]))
                    valuePtr = DataToPtr((byte[])value, out size);
                else if (value.GetType() == typeof(double))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(float))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(int))
                    valuePtr = IntToPtr(Convert.ToInt32(value), out size);
                else
                    return Result.Failure;
                Result res = igs_attribute_create(nameAsPtr, type, valuePtr, size);
                Marshal.FreeHGlobal(nameAsPtr);
                Marshal.FreeHGlobal(valuePtr);
                return res;
            }
            else
            {
                Result res = igs_attribute_create(nameAsPtr, type, IntPtr.Zero, 0);
                Marshal.FreeHGlobal(nameAsPtr);
                return res;
            }
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_remove(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Result AttributeRemove(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result res = igs_attribute_remove(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IopValueType igs_attribute_type(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IopValueType AttributeType(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IopValueType type = igs_attribute_type(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return type;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_attribute_count();

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <returns></returns>
        public static int AttributeCount() { return igs_attribute_count(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_attribute_list(ref int nbOfElements);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <returns></returns>
        public static string[] AttributeList()
        {
            int nbOfElements = 0;
            string[] list = null;
            IntPtr intptr = igs_attribute_list(ref nbOfElements);
            if (intptr != IntPtr.Zero)
            {
                IntPtr[] intPtrArray = new IntPtr[nbOfElements];
                list = new string[nbOfElements];
                Marshal.Copy(intptr, intPtrArray, 0, nbOfElements);
                for (int i = 0; i < nbOfElements; i++)
                    list[i] = Marshal.PtrToStringAnsi(intPtrArray[i]);
                Igs.igs_free_io_list(intptr, nbOfElements);
            }
            return list;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_attribute_exists(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool AttributeExists(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_attribute_exists(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }


        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_attribute_bool(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool AttributeBool(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_attribute_bool(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_attribute_int(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static int AttributeInt(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            int value = igs_attribute_int(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern double igs_attribute_double(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static double AttributeDouble(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            double value = igs_attribute_double(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_attribute_string(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string AttributeString(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = igs_attribute_string(nameAsPtr);
            string value = PtrToStringFromUTF8(valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_data(IntPtr name, ref IntPtr data, ref uint size);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static byte[] AttributeData(string name)
        {
            uint size = 0;
            byte[] data = null;
            IntPtr ptr = IntPtr.Zero;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result result = igs_attribute_data(nameAsPtr, ref ptr, ref size);
            Marshal.FreeHGlobal(nameAsPtr);
            if (result == Result.Success)
            {
                data = new byte[size];
                if (ptr != IntPtr.Zero)
                    Marshal.Copy(ptr, data, 0, (int)size);
            }
            return data;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_bool(IntPtr name, bool value);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result AttributeSetBool(string name, bool value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_attribute_set_bool(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_int(IntPtr name, int value);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result AttributeSetInt(string name, int value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_attribute_set_int(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_double(IntPtr name, double value);
        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result AttributeSetDouble(string name, double value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_attribute_set_double(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_string(IntPtr name, IntPtr value);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result AttributeSetString(string name, string value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = StringToUTF8Ptr(value);
            result = igs_attribute_set_string(nameAsPtr, valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_data(IntPtr name, IntPtr value, uint size);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result AttributeSetData(string name, byte[] value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            uint size = Convert.ToUInt32(((byte[])value).Length);
            IntPtr valueAsPtr = Marshal.AllocHGlobal((int)size);
            Marshal.Copy(value, 0, valueAsPtr, (int)size);
            result = igs_attribute_set_data(nameAsPtr, valueAsPtr, size);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_add_constraint(IntPtr name, IntPtr constraint);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="constraint"></param>
        /// <returns></returns>
        public static Result AttributeAddConstraint(string name, string constraint)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr constraintAsPtr = StringToUTF8Ptr(constraint);
            result = igs_attribute_add_constraint(nameAsPtr, constraintAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(constraintAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_description(IntPtr name, IntPtr description);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        public static Result AttributeSetDescription(string name, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_attribute_set_description(nameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_attribute_description(IntPtr name);
        public static string AttributeDescription(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = igs_attribute_description(nameAsPtr);
            string res = PtrToStringFromUTF8(descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_attribute_set_detailed_type(IntPtr paramName, IntPtr typeName, IntPtr specification);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        public static Result AttributeSetDetailedType(string paramName, string typeName, string specification)
        {
            IntPtr paramNameAsPtr = StringToUTF8Ptr(paramName);
            IntPtr typeAsPtr = StringToUTF8Ptr(typeName);
            IntPtr specificationAsPtr = StringToUTF8Ptr(specification);
            Result res = igs_attribute_set_detailed_type(paramNameAsPtr, typeAsPtr, specificationAsPtr);
            Marshal.FreeHGlobal(paramNameAsPtr);
            Marshal.FreeHGlobal(typeAsPtr);
            Marshal.FreeHGlobal(specificationAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_attribute(IntPtr name);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="name"></param>
        public static void ClearAttribute(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            igs_clear_attribute(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_attribute(IntPtr name, IopFunctionC cb, IntPtr myData);

        /// <summary>
        /// <inheritdoc cref="AttributeCreate"/>
        /// </summary>
        /// <param name="ParameterName"></param>
        /// <param name="callback"></param>
        /// <param name="myData"></param>
        public static void ObserveAttribute(string ParameterName, IopFunction callback, object myData)
        {
            Tuple<IopFunction, object> tupleData = new Tuple<IopFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);

            if (_OnIOPCallback == null)
                _OnIOPCallback = OnIOPCallback;

            IntPtr nameAsPtr = StringToUTF8Ptr(ParameterName);
            igs_observe_attribute(nameAsPtr, _OnIOPCallback, data);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        #endregion

        #endregion

        #region Mapping edition & inspection

            #region load / set / get mapping

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_mapping_load_str(IntPtr json_str);
        public static Result MappingLoadStr(string json_str)
        {
            IntPtr jsonAsPtr = StringToUTF8Ptr(json_str);
            Result res = igs_mapping_load_str(jsonAsPtr);
            Marshal.FreeHGlobal(jsonAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_mapping_load_file(IntPtr file_path);
        public static Result MappingLoadFile(string file_path)
        {
            IntPtr pathAsPtr = StringToUTF8Ptr(file_path);
            Result res = igs_mapping_load_file(pathAsPtr);
            Marshal.FreeHGlobal(pathAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_mapping_json();
        public static string MappingJson()
        {
            IntPtr ptr = igs_mapping_json();
            return (ptr == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igs_mapping_count();

        /// <summary>
        /// number of entries in the mapping output type
        /// </summary>
        /// <returns></returns>
        public static uint MappingCount() { return igs_mapping_count(); }

        #endregion

            #region clear Mappings

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_mappings();

        /// <summary>
        /// clears all our mappings with all agents
        /// </summary>
        public static void ClearMappings() { igs_clear_mappings(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_mappings_with_agent(IntPtr agentName);

        /// <summary>
        /// clears our mappings with this agent
        /// </summary>
        /// <param name="agentName"></param>
        public static void ClearMappingsWithAgent(string agentName)
        {
            IntPtr ptrAgentName = StringToUTF8Ptr(agentName);
            igs_clear_mappings_with_agent(ptrAgentName);
            Marshal.FreeHGlobal(ptrAgentName);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_mappings_for_input(IntPtr inputName);

        /// <summary>
        /// clear all mappings for this input
        /// </summary>
        /// <param name="inputName"></param>
        public static void ClearMappingsForInput(string inputName)
        {
            IntPtr ptrInputName = StringToUTF8Ptr(inputName);
            igs_clear_mappings_for_input(ptrInputName);
            Marshal.FreeHGlobal(ptrInputName);
        }

        #endregion

            #region edit mappings

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong igs_mapping_add(IntPtr fromOurInput, IntPtr toAgent, IntPtr withOutput);

        /// <summary>
        /// returns mapping id or zero if creation failed
        /// </summary>
        /// <param name="fromOurInput"></param>
        /// <param name="toAgent"></param>
        /// <param name="withOutput"></param>
        /// <returns></returns>
        public static ulong MappingAdd(string fromOurInput, string toAgent, string withOutput)
        {
            IntPtr fromOurInputAsPtr = StringToUTF8Ptr(fromOurInput);
            IntPtr toAgentAsPtr = StringToUTF8Ptr(toAgent);
            IntPtr withOutputAsPtr = StringToUTF8Ptr(withOutput);
            ulong id = igs_mapping_add(fromOurInputAsPtr, toAgentAsPtr, withOutputAsPtr);
            Marshal.FreeHGlobal(fromOurInputAsPtr);
            Marshal.FreeHGlobal(toAgentAsPtr);
            Marshal.FreeHGlobal(withOutputAsPtr);
            return id;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_mapping_remove_with_id(ulong theId);
        public static Result MappingRemoveWithId(ulong theId) { return igs_mapping_remove_with_id(theId); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_mapping_remove_with_name(IntPtr fromOurInput, IntPtr toAgent, IntPtr withOutput);
        public static Result MappingRemoveWithName(string fromOurInput, string toAgent, string withOutput)
        {
            IntPtr fromOurInputAsPtr = StringToUTF8Ptr(fromOurInput);
            IntPtr toAgentAsPtr = StringToUTF8Ptr(toAgent);
            IntPtr withOutputAsPtr = StringToUTF8Ptr(withOutput);
            Result res = igs_mapping_remove_with_name(fromOurInputAsPtr, toAgentAsPtr, withOutputAsPtr);
            Marshal.FreeHGlobal(fromOurInputAsPtr);
            Marshal.FreeHGlobal(toAgentAsPtr);
            Marshal.FreeHGlobal(withOutputAsPtr);
            return res;
        }

        #endregion

            #region edit splits

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igs_split_count();

        /// <summary>
        /// number of splits entries
        /// </summary>
        /// <returns></returns>
        public static uint SplitCount() { return igs_split_count(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong igs_split_add(IntPtr fromOurInput, IntPtr toAgent, IntPtr withOutput);

        /// <summary>
        /// returns split id or zero if creation failed
        /// </summary>
        /// <param name="fromOurInput"></param>
        /// <param name="toAgent"></param>
        /// <param name="withOutput"></param>
        /// <returns></returns>
        public static ulong SplitAdd(string fromOurInput, string toAgent, string withOutput)
        {
            IntPtr ptrFromOurInput = StringToUTF8Ptr(fromOurInput);
            IntPtr ptrToAgent = StringToUTF8Ptr(toAgent);
            IntPtr ptrWithOutput = StringToUTF8Ptr(withOutput);
            ulong result = igs_split_add(ptrFromOurInput, ptrToAgent, ptrWithOutput);
            Marshal.FreeHGlobal(ptrFromOurInput);
            Marshal.FreeHGlobal(ptrToAgent);
            Marshal.FreeHGlobal(ptrWithOutput);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_split_remove_with_id(ulong id);
        public static Result SplitRemoveWithId(ulong id) { return igs_split_remove_with_id(id); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_split_remove_with_name(IntPtr fromOurInput, IntPtr toAgent, IntPtr withOutput);
        public static Result SplitRemoveWithName(string fromOurInput, string toAgent, string withOutput)
        {
            IntPtr ptrFromOurInput = StringToUTF8Ptr(fromOurInput);
            IntPtr ptrToAgent = StringToUTF8Ptr(toAgent);
            IntPtr ptrWithOutput = StringToUTF8Ptr(withOutput);
            Result result = igs_split_remove_with_name(ptrFromOurInput, ptrToAgent, ptrWithOutput);
            Marshal.FreeHGlobal(ptrFromOurInput);
            Marshal.FreeHGlobal(ptrToAgent);
            Marshal.FreeHGlobal(ptrWithOutput);
            return result;
        }

        #endregion

            #region mapping other agents
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_mapping_set_outputs_request(bool notify);
        /// <summary>
        /// When mapping other agents, it is possible to request the
        /// mapped agents to send us their current output values
        /// through a private communication for our proper initialization.
        /// By default, this behavior is disabled. 
        /// </summary>
        public static void MappingSetOutputsRequest(bool notify) { igs_mapping_set_outputs_request(notify); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_mapping_outputs_request();

        /// <summary>
        /// <inheritdoc cref="MappingSetOutputsRequest"/>
        /// </summary>
        /// <returns></returns>
        public static bool MappingOutputsRequest() { return Convert.ToBoolean(igs_mapping_outputs_request()); }
        #endregion

        #endregion

        #region  Timers 

        private static TimerFunctionC _OnTimer;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void TimerFunctionC(int timerId, IntPtr myData);
        public delegate void TimerFunction(int timerId, object myData);

        static void OnTimer(int timerId, IntPtr myData)
        {
            GCHandle gCHandleData = GCHandle.FromIntPtr(myData);
            Tuple<TimerFunction, object> tuple = (Tuple<TimerFunction, object>)gCHandleData.Target;
            object data = tuple.Item2;
            TimerFunction cSharpFunction = tuple.Item1;
            cSharpFunction(timerId, data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_timer_start(UIntPtr delay, UIntPtr times, TimerFunctionC cb, IntPtr myData);
        /// <summary>
        /// Timers can be created to call code a certain number of times,
        /// each time after a certain delay. 0 times means repeating forever.<br />
        /// Delay is expressed in milliseconds.<br />
        /// WARNING: Timers MUST be created after starting an agent.<br />
        /// </summary>
        public static int TimerStart(uint delay, uint times, TimerFunction cbsharp, object myData)
        {
            if (cbsharp != null)
            {
                Tuple<TimerFunction, object> tupleData = new Tuple<TimerFunction, object>(cbsharp, myData);
                GCHandle gCHandle = GCHandle.Alloc(tupleData);
                IntPtr data = GCHandle.ToIntPtr(gCHandle);
                if (_OnTimer == null)
                    _OnTimer = OnTimer;
                
                return igs_timer_start((UIntPtr)delay, (UIntPtr)times, _OnTimer, data);
            }
            else
                return -1;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_timer_stop(int timerId);

        /// <summary>
        /// <inheritdoc cref="TimerStart"/>
        /// </summary>
        /// <param name="timerId"></param>
        public static void TimerStop(int timerId) { igs_timer_stop(timerId); }

        #endregion

        #region Communicating via channels (a.k.a Zyre groups and peers)

            #region send message to a channel
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_channel_shout_str(IntPtr channel, IntPtr msg);
        public static Result ChannelShout(string channel, string msg) 
        {
            Result result;
            IntPtr channelAsPtr = StringToUTF8Ptr(channel);
            IntPtr messageAsPtr = StringToUTF8Ptr(msg);
            result = igs_channel_shout_str(channelAsPtr, messageAsPtr);
            Marshal.FreeHGlobal(channelAsPtr);
            Marshal.FreeHGlobal(messageAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_channel_shout_data(IntPtr channel, IntPtr msg, uint size);
        public static Result ChannelShout(string channel, byte[] data)
        {
            Result result;
            IntPtr channelAsPtr = StringToUTF8Ptr(channel);
            uint size;
            IntPtr dataPtr = DataToPtr(data, out size);
            result = igs_channel_shout_data(channelAsPtr, dataPtr, size);
            Marshal.FreeHGlobal(channelAsPtr);
            Marshal.FreeHGlobal(dataPtr);
            return result;
        }
        #endregion

            #region send a message to an agent by name or by uuid
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_channel_whisper_str(IntPtr agentNameOrUUID, IntPtr msg);
        /// <summary>
        /// send a message to an agent by name or by uuid <br />
        /// NB: peer ids and names are also supported by these functions but are used only if no agent is found first<br />
        /// NB: if several agents share the same name, all will receive the message if addressed by name<br />
        /// </summary>
        /// <param name="agentNameOrUUID"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static Result ChannelWhisper(string agentNameOrUUID, string msg)
        {
            Result result;
            IntPtr agentAsPtr = StringToUTF8Ptr(agentNameOrUUID);
            IntPtr messageAsPtr = StringToUTF8Ptr(msg);
            result = igs_channel_whisper_str(agentAsPtr, messageAsPtr);
            Marshal.FreeHGlobal(agentAsPtr);
            Marshal.FreeHGlobal(messageAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_channel_whisper_data(IntPtr agentNameOrUUID, IntPtr msg, uint size);

        /// <summary>
        /// <inheritdoc cref="ChannelWhisper"/>
        /// </summary>
        /// <param name="agentNameOrUUID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Result ChannelWhisper(string agentNameOrUUID, byte[] data)
        {
            Result result;
            IntPtr agentAsPtr = StringToUTF8Ptr(agentNameOrUUID);
            uint size;
            IntPtr dataPtr = DataToPtr(data, out size);
            result = igs_channel_whisper_data(agentAsPtr, dataPtr, size);
            Marshal.FreeHGlobal(agentAsPtr);
            Marshal.FreeHGlobal(dataPtr);
            return result;
        }
        #endregion

            #region set zyre headers
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_peer_add_header(IntPtr key, IntPtr value);
        public static Result PeerAddHeader(string key, string value)
        {
            Result result;
            IntPtr keyAsPtr = StringToUTF8Ptr(key);
            IntPtr valueAsPtr = StringToUTF8Ptr(value);
            result = igs_peer_add_header(keyAsPtr, valueAsPtr);
            Marshal.FreeHGlobal(keyAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_peer_remove_header(IntPtr key);
        public static Result PeerRemoveHeader(string key)
        {
            Result result;
            IntPtr keyAsPtr = StringToUTF8Ptr(key);
            result = igs_peer_remove_header(keyAsPtr);
            Marshal.FreeHGlobal(keyAsPtr);
            return result;
        }
        #endregion

        #endregion

        #region BROKERS VS. SELF-DISCOVERY

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_broker_add(IntPtr brokerEndpoint);
        /// <summary>
        /// <para>igs_start_with_device and igs_start_with_ip enable the agents to self-discover
        /// using UDP broadcast messages on the passed port.UDP broadcast messages can
        /// be blocked on some networks and can make things complex on networks with
        /// sub-networks.<br />
        /// That is why ingescape also supports the use of brokers to relay discovery
        /// using TCP connections.Any agent can be a broker and agents using brokers
        /// simply have to use a list of broker endpoints.One broker is enough but
        /// several brokers can be set for robustness.</para>
        /// 
        /// <para>For clarity, it is better if brokers are well identified on your platform,
        /// started before any agent, and serve only as brokers.But any other architecture
        /// is permitted and brokers can be restarted at any time.</para>
        /// 
        /// <para>Endpoints have the form tcp://ip_address:port<br />
        /// • igs_brokers_add is used to add brokers to connect to.Add
        /// as many brokers as you want.At least one declared broker is necessary to
        /// use igs_start_with_brokers. Use igs_clear_brokers to remove all the current
        /// brokers.<br />
        ///  • The endpoint in igs_broker_set_endpoint is the broker address we should be reached
        /// at as a broker if we want to be one.Using igs_broker_set_endpoint makes us a broker
        /// when starting.<br />
        /// • The endpoint in igs_broker_set_advertized_endpoint replaces the one declared in
        /// igs_start_with_brokers for the registration to the brokers.This function enables
        /// passing through NAT and using a public address.Attention: this public address
        ///  shall make sense to all the agents that will connect to us, independently from
        /// their local network.<br />
        /// • Our agent endpoint in igs_start_with_brokers gives the address and port our
        /// agent can be reached at.This endpoint must be valid in the actual network
        /// configuration.</para>
        /// </summary>
        public static Result BrokerAdd(string brokerEndpoint)
        {
            IntPtr brokerEndpointAsPtr = StringToUTF8Ptr(brokerEndpoint);
            Result res = igs_broker_add(brokerEndpointAsPtr);
            Marshal.FreeHGlobal(brokerEndpointAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_brokers();

        /// <summary>
        /// <inheritdoc cref="BrokerAdd"/>
        /// </summary>
        public static void ClearBrokers() { igs_clear_brokers(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_broker_enable_with_endpoint(IntPtr ourBrokerEndpoint);

        /// <summary>
        /// <inheritdoc cref="BrokerAdd"/>
        /// </summary>
        public static void BrokerEnableWithEndpoint(string ourBrokerEndpoint)
        {
            IntPtr ourBrokerEndpointAsPtr = StringToUTF8Ptr(ourBrokerEndpoint);
            igs_broker_enable_with_endpoint(ourBrokerEndpointAsPtr);
            Marshal.FreeHGlobal(ourBrokerEndpointAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_broker_set_advertized_endpoint(IntPtr advertisedEndpoint);

        /// <summary>
        /// <inheritdoc cref="BrokerAdd"/>
        /// </summary>
        /// <param name="advertisedEndpoint"> can be null</param>
        public static void BrokerSetAdvertizedEndpoint(string advertisedEndpoint)
        {
            if (advertisedEndpoint == null)
                igs_broker_set_advertized_endpoint(IntPtr.Zero);
            else
            {
                IntPtr advertisedEndpointAsPtr = StringToUTF8Ptr(advertisedEndpoint);
                igs_broker_set_advertized_endpoint(advertisedEndpointAsPtr);
                Marshal.FreeHGlobal(advertisedEndpointAsPtr);
            }
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_start_with_brokers(IntPtr agentEndpoint);

        /// <summary>
        /// <inheritdoc cref="BrokerAdd"/>
        /// </summary>
        public static Result StartWithBrokers(string agentEndpoint)
        {
            IntPtr agentEndpointAsPtr = StringToUTF8Ptr(agentEndpoint);
            Result res = igs_start_with_brokers(agentEndpointAsPtr);
            Marshal.FreeHGlobal(agentEndpointAsPtr);
            return res;
        }

        #endregion

        #region  Security : identity, end-to-end encryption

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_enable_security(IntPtr privateKeyFile, IntPtr publicKeysDirectory);
        /// <summary>
        /// <para>Security is about authentification of other agents and encrypted communications.
        ///  Both are offered by Ingescape using a public/private keys mechanism relying on ZeroMQ.
        ///  Security is activated optionally.<br />
        ///  • If public/private keys are generated on the fly, one obtains the same protection as TLS
        ///  for HTTPS communications.Thirdparties cannot steal identities and communications are
        ///  encrypted end-to-end.But any Ingescape agent with security enabled can join a platform.<br />
        ///  • If public/private keys are stored locally by each agent, no thirdparty can join a platform
        ///  without having a public key that is well-known by the other agents.This is safer but requires
        /// securing and synchronizing local files with each agent accessing its private key and public
        /// keys of other agents.</para>
        /// 
        /// <para>Security is enabled by calling igs_enable_security.<br />
        ///  • If privateKey is null, our private key is generated on the fly and any agent with
        ///  security enabled will be able to connect, publicKeysDirectory will be ignored.<br />
        ///  • If privateKey is NOT null, private key at privateKey path will be used and only
        ///  agents whose public keys are in publicKeysDirectory will be able to connect.<br />
        ///  NB: if privateKey is NOT null and publicKeysDirectory is null or does not exist,
        ///  security will not be enabled and our agent will not start.</para>
        /// </summary>
        public static Result EnableSecurity(string privateKeyFile, string publicKeysDirectory)
        {
            IntPtr privateKeyFileAsPtr = StringToUTF8Ptr(privateKeyFile);
            IntPtr publicKeysDirectoryAsPtr = StringToUTF8Ptr(publicKeysDirectory);
            Result res = igs_enable_security(privateKeyFileAsPtr, publicKeysDirectoryAsPtr);
            Marshal.FreeHGlobal(privateKeyFileAsPtr);
            Marshal.FreeHGlobal(publicKeysDirectoryAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_disable_security();

        /// <summary>
        /// <inheritdoc cref="EnableSecurity"/>
        /// </summary>
        public static void DisableSecurity() { igs_disable_security(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_broker_add_secure(IntPtr brokerEndpoint, IntPtr publicKeyPath);

        /// <summary>
        /// <inheritdoc cref="EnableSecurity"/>
        /// </summary>
        public static Result BrokerAddSecure(string brokerEndpoint, string publicKeyPath)
        {
            IntPtr brokerEndpointAsPtr = StringToUTF8Ptr(brokerEndpoint);
            IntPtr publicKeyPathAsPtr = StringToUTF8Ptr(publicKeyPath);
            Result res = igs_broker_add_secure(brokerEndpointAsPtr, publicKeyPathAsPtr);
            Marshal.FreeHGlobal(brokerEndpointAsPtr);
            Marshal.FreeHGlobal(publicKeyPathAsPtr);
            return res;
        }

        #endregion

        #region Elections and leadership between agents

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_election_join(IntPtr electionName);
        /// <summary>
        /// Create named elections between agents and designate a winner,
        ///  as soon as they are two agents or more participating.<br />
        ///  • AgentWonElection agent event means that the election is over and this agent has WON<br />
        ///  • AgentLostElection agent event means that the election is over and this agent has LOST<br />
        ///  • The election happens only when at least two agents participate.<br />
        /// Nothing happens if only one agent participates.<br />
        ///  • When only one agent remains in an election after several have
        /// joined and left, it is declared winner.<br />
        /// At startup, it is up to the developer to decide if an agent shall be
        /// considered as winner or wait for a certain amount of time to trigger
        /// some behavior.Do not forget that elections take at least some
        /// millisconds to be concluded.<br />
        /// Agents in the same peer cannot compete one with another. Elections are
        /// reserved to agents running on separate peers/processes.If several
        /// agents in the same peer participate in the same election, they will
        /// all be declared winners or losers all together.<br />
        /// The AgentWonElection and AgentLostElection agent events
        /// can be triggered MULTIPLE TIMES in a row. Please adjust your agent
        /// behavior accordingly.<br />
        /// </summary>
        public static Result ElectionJoin(string electionName)
        {
            IntPtr electionNameAsPtr = StringToUTF8Ptr(electionName);
            Result res = igs_election_join(electionNameAsPtr);
            Marshal.FreeHGlobal(electionNameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_election_leave(IntPtr electionName);

        /// <summary>
        /// <inheritdoc cref="ElectionJoin"/>
        /// </summary>
        /// <param name="electionName"></param>
        /// <returns></returns>
        public static Result ElectionLeave(string electionName)
        {
            IntPtr electionNameAsPtr = StringToUTF8Ptr(electionName);
            Result res = igs_election_leave(StringToUTF8Ptr(electionName));
            Marshal.FreeHGlobal(electionNameAsPtr);
            return res;
        }

        #endregion

        #region Ingescape real-time communications

            #region GET TIMESTAMP FOR RECEIVED INPUTS AND SERVICES

        /// <summary>
        /// Ingescape is a reactive communication library but it is capable to
        /// handle soft real-time communications and provides functions dedicated
        /// to time management with or without a master clock involved.
        /// </summary>
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]        
        private static extern int igs_rt_get_current_timestamp();

        /// <summary>
        /// <inheritdoc cref="igs_rt_get_current_timestamp"/> <br />
        /// When observing an input or a service, call this function inside the callback
        /// to get the current timestamp in microseconds for the received information.
        /// NB: if timestamp is not available in received input or service, current
        /// time in microseconds is set to INT64_MIN.
        /// </summary>
        public static int RtGetCurrentTimestamp() { return igs_rt_get_current_timestamp(); }

        #endregion

            #region ENABLE TIMESTAMPS IN OUR AGENT FOR PUBLISHED OUTPUTS AND SERVICE CALLS

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_rt_set_timestamps(bool enable);
        /// <summary>
        /// <inheritdoc cref="igs_rt_get_current_timestamp"/> <br />
        /// When timestamps are enabled, every output publication and every service call
        /// carry an additional information providing the timestamp of the message on
        /// the sender side. On the receiver side, timestamp is obtained by calling
        /// igs_rt_get_current_timestamp
        /// </summary>
        public static void RtSetTimestamps(bool enable) { igs_rt_set_timestamps(enable); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_rt_timestamps();

        /// <summary>
        /// <inheritdoc cref="RtSetTimestamps"/>
        /// </summary>
        /// <returns></returns>
        public static bool RtTimestamps() { return igs_rt_timestamps(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_rt_set_time(int microseconds);
        #endregion

            #region SET TIME MANUALLY FOR TIMESTAMPED PUBLISHED OUTPUTS AND SERVICES

        /// <summary>
        /// <inheritdoc cref="igs_rt_get_current_timestamp"/> <br />
        /// When a master clock is involed(e.g.linked to an input of an agent), it
        /// is possible to override the automatic timestamp mechanism to force a value
        /// for the current time in microseconds.<br />
        /// Once igs_rt_set_time has been called, it is necessary to continue calling it
        /// periodically and manually to update the agent's current time in microseconds.
        /// NB : a call to igs_rt_set_time autmatically enables timestamps for outputs
        /// and services on all agents in our process.Timestamps cannot be disabled afterwards.
        /// NB : igs_rt_set_time and igs_rt_time operate at peer level for all the agents
        /// in the process. All agents in a process use the same time set by igs_rt_set_time.
        /// </summary>
        public static void RtSetTime(int microseconds) { igs_rt_set_time(microseconds); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_rt_time();

        /// <summary>
        /// <inheritdoc cref="RtSetTime"/>
        /// </summary>
        /// <returns></returns>
        public static int RtTime() { return igs_rt_time(); }

        #endregion

            #region ENABLE SYNCHRONOUS MODE

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_rt_set_synchronous_mode(bool enable);

        /// <summary>
        /// <inheritdoc cref="igs_rt_get_current_timestamp"/> <br />
        /// When this mode is enabled, outputs are published only when igs_rt_set_time
        /// is called.The call to igs_rt_set_time is the trigger for output publication
        /// in this synchronous real-time mode.All published outputs are timestamped
        /// with the value set by igs_rt_set_time.<br />
        /// NB: Ingescape services and channels are not affected by the synchronous mode.<br />
        /// NB: This mode is set at agent level.
        /// </summary>
        public static void RtSetSynchronousMode(bool enable) { igs_rt_set_synchronous_mode(enable); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_rt_synchronous_mode();

        /// <summary>
        /// <inheritdoc cref="RtSetSynchronousMode"/>
        /// </summary>
        /// <returns></returns>
        public static bool RtSynchronousMode() { return igs_rt_synchronous_mode(); }
        #endregion

        #endregion

        #region Administration, logging, configuration and utilities

            #region LOG ALIASES

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log(LogLevel logLevel, IntPtr function, IntPtr message);

        /// <summary>
        /// LOGS POLICY <br />
        /// - fatal : Events that force application termination.<br />
        /// - error : Events that are fatal to the current operation but not the whole application.<br />
        /// - warning : Events that can potentially cause application anomalies but that can be recovered automatically (by circumventing or retrying).<br />
        /// - info : Generally useful information to log (service start/stop, configuration assumptions, etc.).<br />
        /// - debug : Information that is diagnostically helpful to people more than just developers but useless for system monitoring.<br />
        ///  - trace : Information about parts of functions, for detailed diagnostic only.<br />
        /// </summary>
        public static void Log(LogLevel logLevel, string function, string message) { igs_log(logLevel, StringToUTF8Ptr(function), StringToUTF8Ptr(message)); }

        public static void Trace(string message, [CallerMemberName] string memberName = "") { Log(LogLevel.LogTrace, memberName, message); }
        public static void Debug(string message, [CallerMemberName] string memberName = "") { Log(LogLevel.LogDebug, memberName, message); }
        public static void Info(string message, [CallerMemberName] string memberName = "") { Log(LogLevel.LogInfo, memberName, message); }
        public static void Warn(string message, [CallerMemberName] string memberName = "") { Log(LogLevel.LogWarn, memberName, message); }
        public static void Error(string message, [CallerMemberName] string memberName = "") { Log(LogLevel.LogError, memberName, message); }
        public static void Fatal(string message, [CallerMemberName] string memberName = "") { Log(LogLevel.LogFatal, memberName, message); }

        #endregion

            #region PROTOCOL AND REGION

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_version();
        public static int Version() { return igs_version(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_protocol();
        public static int Protocol() { return igs_protocol(); }
        #endregion

            #region COMMAND LINE

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_set_command_line(IntPtr line);
        /// <summary>
        /// Agent command line can be passed here to be used by ingescapeLauncher. If not set,
        /// command line is initialized with exec path without any attribute.
        /// </summary>
        public static void SetCommandLine(string line)
        {
            IntPtr lineAsPtr = StringToUTF8Ptr(line);
            igs_set_command_line(lineAsPtr);
            Marshal.FreeHGlobal(lineAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_set_command_line_from_args(int argc, IntPtr argv); //first element is replaced by absolute exec path on UNIX
        /// <summary>
        /// <inheritdoc cref="SetCommandLine"/>
        /// </summary>
        public static void SetCommandLineFromArgs(string[] argv)
        {
            int argc = argv.Length;
            IntPtr argvAsPtr = Marshal.AllocCoTaskMem(argc * IntPtr.Size);
            IntPtr[] argvArray = new IntPtr[argv.Length];

            int index = 0;
            foreach (string arg in argv)
            {
                IntPtr argPtr = StringToUTF8Ptr(arg);
                argvArray[index] = argPtr;
                index++;
            }
            Marshal.Copy(argvArray, 0, argvAsPtr, argc);
            igs_set_command_line_from_args(argc, argvAsPtr);

            for (int i = 0; i < argc; i++)
            {
                IntPtr argPtr = argvArray[i];
                Marshal.FreeHGlobal(argPtr);
            }

            Marshal.FreeCoTaskMem(argvAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_command_line();
        public static string CommandLine()
        {
            IntPtr lineAsPtr = igs_command_line();
            string result = PtrToStringFromUTF8(lineAsPtr);
            return result;
        }

        #endregion

            #region LOGS MANAGEMENT

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_console(bool verbose);

        /// <summary>
        /// enable logs in console (ERROR and FATAL are always displayed)
        /// </summary>
        /// <param name="verbose"></param>
        public static void LogSetConsole(bool verbose) { igs_log_set_console(verbose); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_log_console();
        public static bool LogConsole() { return igs_log_console(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_syslog(bool useSyslog);

        /// <summary>
        /// enable system logs on UNIX boxes (not working on Windows yet)
        /// </summary>
        /// <param name="useSyslog"></param>
        private static void LogSetSyslog(bool useSyslog) { igs_log_set_syslog(useSyslog); } // private cause not working on Windows yet

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_log_syslog();
        public static bool LogSyslog() { return igs_log_syslog(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_log_console_color();
        public static bool LogConsoleColor() { return igs_log_console_color(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_console_color(bool useColor);

        /// <summary>
        /// use colors in console
        /// </summary>
        /// <param name="useColor"></param>
        public static void LogSetConsoleColor(bool useColor) { igs_log_set_console_color(useColor); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_console_level(LogLevel level);

        /// <summary>
        /// set log level in console, default is IGS_LOG_WARN
        /// </summary>
        /// <param name="level"></param>
        public static void LogSetConsoleLevel(LogLevel level) { igs_log_set_console_level(level); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern LogLevel igs_log_console_level();
        public static LogLevel LogConsoleLevel() { return igs_log_console_level(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_stream(bool useLogStream);

        /// <summary>
        /// enable logs in socket stream
        /// </summary>
        /// <param name="useLogStream"></param>
        public static void LogSetStream(bool useLogStream) { igs_log_set_stream(useLogStream); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_log_stream();
        public static bool LogStream() { return igs_log_stream(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_file(bool useLogFile, IntPtr path);

        /// <summary>
        /// enable logs in file. If path is NULL, uses default path (~/Documents/Ingescape/logs).
        /// </summary>
        /// <param name="useLogFile"></param>
        /// <param name="path"></param>
        public static void LogSetFile(bool useLogFile, string path = null){ igs_log_set_file(useLogFile, StringToUTF8Ptr(path)); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_file_level(LogLevel level);
        public static void LogSetFileLevel(LogLevel level){ igs_log_set_file_level(level); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_file_max_line_length(uint size);
        public static void LogSetFileMaxLineLength(uint size){ igs_log_set_file_max_line_length(size); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_log_file();
        public static bool LogFile() { return igs_log_file(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_set_file_path(string path);

        /// <summary>
        /// default directory is ~/ on UNIX systems and current PATH on Windows
        /// </summary>
        /// <param name="path"></param>
        public static void LogSetFilePath(string path) { igs_log_set_file_path(path); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_log_file_path();
        public static string LogFilePath()
        {
            IntPtr ptr = igs_log_file_path();
            return (ptr == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_include_data(bool enable);

        /// <summary>
        /// log details of data IOs in log files , default is false.
        /// </summary>
        /// <param name="enable"></param>
        public static void LogIncludeData(bool enable) { igs_log_include_data(enable); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_include_services(bool enable);

        /// <summary>
        /// log details about call/excecute services in log files, default is false.
        /// </summary>
        /// <param name="enable"></param>
        public static void LogIncludeServices(bool enable) { igs_log_include_services(enable); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_log_no_warning_if_undefined_service(bool enable);

        /// <summary>
        /// warns or not if an unknown service is called on this agent, default is warning (false).
        /// </summary>
        /// <param name="enable"></param>
        public static void LogNoWarningIfUndefinedService(bool enable) { igs_log_no_warning_if_undefined_service(enable); }

        #endregion

            #region DEFINITION & MAPPING FILE MANAGEMENT

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_definition_set_path(IntPtr path);

        /// <summary>
        /// Enable to write definition and mapping on disk
        /// for our agent.Definition and mapping paths are initialized with
        /// igs_definition_load_file and igs_mappings_load_file.But they can
        /// also be configured using these functions to store current definitions.
        /// </summary>
        /// <param name="path"></param>
        public static void DefinitionSetPath(string path)
        {
            IntPtr ptrPath = StringToUTF8Ptr(path);
            igs_definition_set_path(ptrPath);
            Marshal.FreeHGlobal(ptrPath);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_definition_save();

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPath"/>
        /// </summary>
        public static void DefinitionSave() { igs_definition_save(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_mapping_set_path(IntPtr path);

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPath"/>
        /// </summary>
        /// <param name="path"></param>
        public static void MappingSetPath(string path)
        {
            IntPtr ptrPath = StringToUTF8Ptr(path);
            igs_mapping_set_path(ptrPath);
            Marshal.FreeHGlobal(ptrPath);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_mapping_save();

        /// <summary>
        /// <inheritdoc cref="DefinitionSetPath"/>
        /// </summary>
        public static void MappingSave() { igs_mapping_save(); }
        #endregion

            #region ADVANCED TRANSPORTS

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_set_ipc(bool allow);

        /// <summary>
        /// Ingescape automatically detects agents on the same computer
        /// and then uses optimized inter-process communication protocols
        /// depending on the operating system. <br />
        /// On Microsoft Windows systems, the loopback is used. <br />
        /// Advanced transports are allowed by default and can be disabled <br />
        /// default is true
        /// </summary>
        public static void SetIpc(bool allow) { igs_set_ipc(allow); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_has_ipc();

        /// <summary>
        /// <inheritdoc cref="SetIpc"/>
        /// </summary>
        /// <returns></returns>
        public static bool HasIpc() { return igs_has_ipc(); }

        #endregion

            #region NETWORK DEVICES

        [DllImport(ingescapeDLLPath, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_net_devices_list(ref int nb);

        /// <summary>
        /// detect network adapters with broadcast capabilities
        /// to be used in StartWithDevice
        /// </summary>
        /// <returns></returns>
        public static string[] NetDevicesList()
        {
            int nb = 0;
            IntPtr ptrDevices = igs_net_devices_list(ref nb);
            IntPtr[] ptrArrayOfDevices = new IntPtr[nb];
            Marshal.Copy(ptrDevices, ptrArrayOfDevices, 0, nb);
            string[] devicesArray = new string[nb];
            for (int i = 0; i < nb; i++)
            {
                string isoString = PtrToStringFromISO(ptrArrayOfDevices[i]);
                devicesArray[i] = isoString;
            }

            igs_free_net_devices_list(ptrDevices, nb);
            return devicesArray;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_free_net_devices_list(IntPtr devices, int nb);

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_net_addresses_list(ref int nb);

        /// <summary>
        /// <inheritdoc cref="NetDevicesList"/>
        /// </summary>
        /// <returns></returns>
        public static string[] NetAddressesList()
        {
            int nb = 0;
            IntPtr ptrAddresses = igs_net_addresses_list(ref nb);
            IntPtr[] ptrArrayOfAddresses = new IntPtr[nb];
            Marshal.Copy(ptrAddresses, ptrArrayOfAddresses, 0, nb);
            string[] addressesArray = new string[nb];
            for (int i = 0; i < nb; i++)
            {
                addressesArray[i] = Marshal.PtrToStringAnsi(ptrArrayOfAddresses[i]);
            }

            igs_free_net_addresses_list(ptrAddresses, nb);
            return addressesArray;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_free_net_addresses_list(IntPtr addresses, int nb);

        #endregion

            #region NETWORK CONFIGURATION

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_set_publishing_port(uint port);
        public static void NetSetPublishingPort(uint port) { igs_net_set_publishing_port(port); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_set_log_stream_port(uint port);
        public static void NetSetLogStreamPort(uint port) { igs_net_set_log_stream_port(port); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_set_discovery_interval(uint interval);

        /// <summary>
        /// in milliseconds
        /// </summary>
        /// <param name="interval""></param>
        public static void NetSetDiscoveryInterval(uint interval) { igs_net_set_discovery_interval(interval); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_set_timeout(uint duration);

        /// <summary>
        /// in milliseconds
        /// </summary>
        /// <param name="duration""></param>
        public static void NetSetTimeout(uint duration) { igs_net_set_timeout(duration); }
        
        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_raise_sockets_limit();

        /// <summary>
        /// UNIX only, to be called before any ingescape or ZeroMQ activity
        /// </summary>
        private static void NetRaiseSocketsLimit() { igs_net_raise_sockets_limit(); }  // private cause unix only

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_set_high_water_marks(int hwmValue);
        /// <summary>
        /// Set high water marks (HWM) for the publish/subscribe sockets.<br />
        /// Setting HWM to 0 means that they are disabled.
        /// </summary>
        public static void NetSetHighWaterMarks(int hwmValue) { igs_net_set_high_water_marks(hwmValue); }

        #endregion

            #region SATURATION CONTROL

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_unbind_pipe();

        /// <summary>
        /// In situations where an agent inputs in the peer are excessively
        /// sollicited and it results in even more intensive output publications,
        /// it may saturate the ingescape loop with HANDLE_PUBLICATION messages,
        /// which will end up reaching more than 1000 messages, corresponding to
        /// the default High Water Marks on the pipe PAIR socket.The saturated
        /// PAIR socket will then block and freeze the agent.
        /// We allow here to remove the HWM and to print in real-time the number
        /// of HANDLE_PUBLICATION message stacked in the pipe.
        /// Please note, that disabling the HWM may induce a memory exhaustion
        /// for the agent and the operating system: USE WITH CAUTION.
        /// </summary>
        public static void UnbindPipe()
        {
            igs_unbind_pipe();
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_monitor_pipe_stack(bool monitor); //default is false

        /// <summary>
        /// <inheritdoc cref="UnbindPipe"/>
        /// </summary>
        /// <param name="monitor">default is false</param>
        public static void MonitorPipeStack(bool monitor)
        {
            igs_monitor_pipe_stack(monitor);
        }

        #endregion

            #region PERFORMANCE CHECK

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_net_performance_check(IntPtr peerId, uint msgSize, uint nbOfMsg);

        /// <summary>
        /// sends number of messages with defined size and displays performance
        /// information when finished (information displayed as INFO-level Log)
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="msgSize"></param>
        /// <param name="nbOfMsg"></param>
        public static void NetPerformanceCheck(string peerId, uint msgSize, uint nbOfMsg)
        {
            IntPtr peerIdAsPtr = StringToUTF8Ptr(peerId);
            igs_net_performance_check(peerIdAsPtr, msgSize, nbOfMsg);
            Marshal.FreeHGlobal(peerIdAsPtr);
        }

        #endregion

            #region NETWORK MONITORING

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_monitor_start(uint period);//in milliseconds

        /// <summary>
        /// <para> Ingescape provides an integrated ObserveMonitor to detect events relative to the network.<br />
        /// NB: once igs_monitor_start has been called, igs_monitor_stop must be
        /// called to actually Stop the ObserveMonitor. If not stopped, it may cause an Error when
        /// an agent terminates.
        /// </para>
        /// </summary>
        /// <param name="period">in milliseconds</param>
        public static void MonitorStart(uint period) { igs_monitor_start(period); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_monitor_start_with_network(uint period,
                                                   IntPtr networkDevice,
                                                   uint port);

        /// <summary>
        /// <inheritdoc cref="MonitorStart"/>
        /// </summary>
        /// <param name="period"></param>
        /// <param name="networkDevice"></param>
        /// <param name="port"></param>
        public static void MonitorStartWithNetwork(uint period, string networkDevice, uint port)
        {
            IntPtr networkDeviceAsPtr = StringToUTF8Ptr(networkDevice);
            igs_monitor_start_with_network(period, networkDeviceAsPtr, port);
            Marshal.FreeHGlobal(networkDeviceAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_monitor_stop();

        /// <summary>
        /// <inheritdoc cref="MonitorStart"/>
        /// </summary>
        public static void MonitorStop() { igs_monitor_stop(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_monitor_is_running();
        public static bool MonitorIsRunning() { return igs_monitor_is_running(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_monitor_set_start_stop(bool flag);

        /// <summary>
        /// <para> When the ObserveMonitor is started and igs_monitor_set_start_stop is set to true :<br />
        /// - IP change will cause the agent to restart on the new IP(same device, same port)<br />
        /// - Network device disappearance will cause the agent to Stop.Agent will restart when device is back.
        /// </para>
        /// </summary>
        /// <param name="flag"></param>
        public static void MonitorSetStartStop(bool flag) { igs_monitor_set_start_stop(flag); }

        private static MonitorFunctionC _OnMonitorCallback;
        private delegate void MonitorFunctionC(MonitorEvent monitorEvent,
                                    IntPtr device,
                                    IntPtr ipAddress,
                                    IntPtr myData);
        public delegate void MonitorFunction(MonitorEvent monitorEvent,
                                    string device,
                                    string ipAddress,
                                    object myData);

        static void OnMonitorCallback(MonitorEvent monitorEvent,
                                    IntPtr device,
                                    IntPtr ipAddress,
                                    IntPtr myData)
        {
            GCHandle gCHandleData = GCHandle.FromIntPtr(myData);
            Tuple<MonitorFunction, object> tuple = (Tuple<MonitorFunction, object>)gCHandleData.Target;
            object data = tuple.Item2;
            MonitorFunction cSharpFunction = tuple.Item1;
            cSharpFunction(monitorEvent, PtrToStringFromUTF8(device), PtrToStringFromUTF8(ipAddress), data);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_monitor(MonitorFunctionC cb, IntPtr myData);
        public static void ObserveMonitor(MonitorFunction cbsharp, object myData)
        {
            Tuple<MonitorFunction, object> tupleData = new Tuple<MonitorFunction, object>(cbsharp, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);
            if (_OnMonitorCallback == null)
                _OnMonitorCallback = OnMonitorCallback;
            igs_observe_monitor(_OnMonitorCallback, data);
        }

        #endregion

            #region  CLEAN CONTEXT

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_context();

        /// <summary>
        /// Use this function when you absolutely need to clean all the Ingescape content
        /// and you cannot Stop your application to do so.This function SHALL NOT be used
        /// in production environments.
        /// </summary>
        public static void ClearContext() { igs_clear_context(); }

        #endregion

            #region AGENT FAMILY - for licensing puroposes

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_agent_set_family(IntPtr family);

        /// <summary>
        /// 32 characters canonical UUID format is commonly expected,
        /// Default is an empty string. Max length is 64 characters. <br />
        /// The family is used together with an external licensing
        /// mechanism to uniquely identify a given software agent.
        /// </summary>
        /// <param name="family"></param>
        public static void AgentSetFamily(string family)
        {
            IntPtr familyAsPtr = StringToUTF8Ptr(family);
            igs_agent_set_family(familyAsPtr);
            Marshal.FreeHGlobal(familyAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_agent_family();

        /// <summary>
        /// <inheritdoc cref="AgentSetFamily"/>
        /// </summary>
        /// <returns></returns>
        public static string AgentFamily() { return PtrToStringFromUTF8(igs_agent_family()); }

        #endregion

        #endregion
        
        #region DEPRECATED : Parameters

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_create(IntPtr name, IopValueType type, IntPtr value, uint size);

        [Obsolete("this function is deprecated, please use AttributeCreate instead.")]
        public static Result ParameterCreate(string name, IopValueType type, object value = null)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            if (value != null)
            {
                uint size;
                IntPtr valuePtr;
                if (value.GetType() == typeof(string))
                    valuePtr = StringToUTF8Ptr(Convert.ToString(value), out size);
                else if (value.GetType() == typeof(bool))
                    valuePtr = BoolToPtr(Convert.ToBoolean(value), out size);
                else if (value.GetType() == typeof(byte[]))
                    valuePtr = DataToPtr((byte[])value, out size);
                else if (value.GetType() == typeof(double))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(float))
                    valuePtr = DoubleToPtr(Convert.ToDouble(value), out size);
                else if (value.GetType() == typeof(int))
                    valuePtr = IntToPtr(Convert.ToInt32(value), out size);
                else
                    return Result.Failure;
                Result res = igs_parameter_create(nameAsPtr, type, valuePtr, size);
                Marshal.FreeHGlobal(nameAsPtr);
                Marshal.FreeHGlobal(valuePtr);
                return res;
            }
            else
            {
                Result res = igs_parameter_create(nameAsPtr, type, IntPtr.Zero, 0);
                Marshal.FreeHGlobal(nameAsPtr);
                return res;
            }
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_remove(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeRemove instead.")]
        public static Result ParameterRemove(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result res = igs_parameter_remove(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IopValueType igs_parameter_type(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeType instead.")]
        public static IopValueType ParameterType(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IopValueType type = igs_parameter_type(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return type;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_parameter_count();

        [Obsolete("this function is deprecated, please use AttributeCount instead.")]
        public static int ParameterCount() { return igs_attribute_count(); }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_parameter_list(ref int nbOfElements);

        [Obsolete("this function is deprecated, please use AttributeList instead.")]
        public static string[] ParameterList()
        {
            int nbOfElements = 0;
            string[] list = null;
            IntPtr intptr = igs_parameter_list(ref nbOfElements);
            if (intptr != IntPtr.Zero)
            {
                IntPtr[] intPtrArray = new IntPtr[nbOfElements];
                list = new string[nbOfElements];
                Marshal.Copy(intptr, intPtrArray, 0, nbOfElements);
                for (int i = 0; i < nbOfElements; i++)
                    list[i] = Marshal.PtrToStringAnsi(intPtrArray[i]);
                Igs.igs_free_io_list(intptr, nbOfElements);
            }
            return list;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_parameter_exists(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeExists instead.")]
        public static bool ParameterExists(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_parameter_exists(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igs_parameter_bool(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeBool instead.")]
        public static bool ParameterBool(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            bool value = igs_parameter_bool(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int igs_parameter_int(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeInt instead.")]
        public static int ParameterInt(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            int value = igs_parameter_int(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern double igs_parameter_double(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeDouble instead.")]
        public static double ParameterDouble(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            double value = igs_parameter_double(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igs_parameter_string(IntPtr name);

        [Obsolete("this function is deprecated, please use AttributeString instead.")]
        public static string ParameterString(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = igs_parameter_string(nameAsPtr);
            string value = PtrToStringFromUTF8(valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            return value;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_data(IntPtr name, ref IntPtr data, ref uint size);

        [Obsolete("this function is deprecated, please use AttributeData instead.")]
        public static byte[] ParameterData(string name)
        {
            uint size = 0;
            byte[] data = null;
            IntPtr ptr = IntPtr.Zero;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            Result result = igs_parameter_data(nameAsPtr, ref ptr, ref size);
            Marshal.FreeHGlobal(nameAsPtr);
            if (result == Result.Success)
            {
                data = new byte[size];
                if (ptr != IntPtr.Zero)
                    Marshal.Copy(ptr, data, 0, (int)size);
            }
            return data;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_bool(IntPtr name, bool value);

        [Obsolete("this function is deprecated, please use AttributeSetBool instead.")]
        public static Result ParameterSetBool(string name, bool value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_parameter_set_bool(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_int(IntPtr name, int value);

        [Obsolete("this function is deprecated, please use AttributeSetInt instead.")]
        public static Result ParameterSetInt(string name, int value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_parameter_set_int(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_double(IntPtr name, double value);

        [Obsolete("this function is deprecated, please use AttributeSetDouble instead.")]
        public static Result ParameterSetDouble(string name, double value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            result = igs_parameter_set_double(nameAsPtr, value);
            Marshal.FreeHGlobal(nameAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_string(IntPtr name, IntPtr value);

        [Obsolete("this function is deprecated, please use AttributeSetString instead.")]
        public static Result ParameterSetString(string name, string value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr valueAsPtr = StringToUTF8Ptr(value);
            result = igs_parameter_set_string(nameAsPtr, valueAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_data(IntPtr name, IntPtr value, uint size);

        [Obsolete("this function is deprecated, please use AttributeSetData instead.")]
        public static Result ParameterSetData(string name, byte[] value)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            uint size = Convert.ToUInt32(((byte[])value).Length);
            IntPtr valueAsPtr = Marshal.AllocHGlobal((int)size);
            Marshal.Copy(value, 0, valueAsPtr, (int)size);
            result = igs_parameter_set_data(nameAsPtr, valueAsPtr, size);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(valueAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_add_constraint(IntPtr name, IntPtr constraint);

        [Obsolete("this function is deprecated, please use AttributeAddConstraint instead.")]
        public static Result ParameterAddConstraint(string name, string constraint)
        {
            Result result = Result.Failure;
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr constraintAsPtr = StringToUTF8Ptr(constraint);
            result = igs_parameter_add_constraint(nameAsPtr, constraintAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(constraintAsPtr);
            return result;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_description(IntPtr name, IntPtr description);

        [Obsolete("this function is deprecated, please use AttributeSetDescription instead.")]
        public static Result ParameterSetDescription(string name, string description)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            IntPtr descriptionAsPtr = StringToUTF8Ptr(description);
            Result res = igs_parameter_set_description(nameAsPtr, descriptionAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
            Marshal.FreeHGlobal(descriptionAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern Result igs_parameter_set_detailed_type(IntPtr paramName, IntPtr typeName, IntPtr specification);

        [Obsolete("this function is deprecated, please use AttributeSetDetailedType instead.")]
        public static Result ParameterSetDetailedType(string paramName, string typeName, string specification)
        {
            IntPtr paramNameAsPtr = StringToUTF8Ptr(paramName);
            IntPtr typeAsPtr = StringToUTF8Ptr(typeName);
            IntPtr specificationAsPtr = StringToUTF8Ptr(specification);
            Result res = igs_parameter_set_detailed_type(paramNameAsPtr, typeAsPtr, specificationAsPtr);
            Marshal.FreeHGlobal(paramNameAsPtr);
            Marshal.FreeHGlobal(typeAsPtr);
            Marshal.FreeHGlobal(specificationAsPtr);
            return res;
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_clear_parameter(IntPtr name);

        [Obsolete("this function is deprecated, please use ClearAttribute instead.")]
        public static void ClearParameter(string name)
        {
            IntPtr nameAsPtr = StringToUTF8Ptr(name);
            igs_clear_parameter(nameAsPtr);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        [DllImport(ingescapeDLLPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void igs_observe_parameter(IntPtr name, IopFunctionC cb, IntPtr myData);

        [Obsolete("this function is deprecated, please use ObserveAttribute instead.")]
        public static void ObserveParameter(string ParameterName, IopFunction callback, object myData)
        {
            Tuple<IopFunction, object> tupleData = new Tuple<IopFunction, object>(callback, myData);
            GCHandle gCHandle = GCHandle.Alloc(tupleData);
            IntPtr data = GCHandle.ToIntPtr(gCHandle);

            if (_OnIOPCallback == null)
                _OnIOPCallback = OnIOPCallback;

            IntPtr nameAsPtr = StringToUTF8Ptr(ParameterName);
            igs_observe_parameter(nameAsPtr, _OnIOPCallback, data);
            Marshal.FreeHGlobal(nameAsPtr);
        }

        #endregion
    }
}
