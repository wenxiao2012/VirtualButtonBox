﻿using System;
using UnityEngine;
using Valve.VR;

public class HOTK_TrackedDeviceManager : MonoBehaviour
{
    public static HOTK_TrackedDeviceManager Instance
    {
        get { return _instance ?? (_instance = new GameObject("HOTK_TrackedDeviceManager", typeof(HOTK_TrackedDeviceManager)) {hideFlags = HideFlags.HideInHierarchy}.GetComponent<HOTK_TrackedDeviceManager>()); }
    }

    public uint LeftIndex
    {
        get
        {
            return _leftIndex;
        }
    }
    public uint RightIndex
    {
        get
        {
            return _rightIndex;
        }
    }
    public uint HMDIndex
    {
        get
        {
            return _hmdIndex;
        }
    }

    private static HOTK_TrackedDeviceManager _instance;

    private uint _leftIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
    private uint _rightIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
    private uint _hmdIndex = OpenVR.k_unTrackedDeviceIndexInvalid;

    public void Awake()
    {
        FindControllers();
        FindHMD();
    }

    public void Update()
    {
        FindControllers();
        FindHMD();
        UpdatePoses();
    }

    private readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    private readonly TrackedDevicePose_t[] _gamePoses = new TrackedDevicePose_t[0];

    /// <summary>
    /// Grab the last poses from the compositor and push them to the event listeners.
    /// This method should be disabled if SteamVR_Render.cs is being used, because it is also called there.
    /// </summary>
    private void UpdatePoses()
    {
        var compositor = OpenVR.Compositor;
        if (compositor == null) return;
        compositor.GetLastPoses(_poses, _gamePoses);
        SteamVR_Utils.Event.Send("new_poses", _poses);
        SteamVR_Utils.Event.Send("new_poses_applied");
    }
    
    /// <summary>
    /// Attempt to find the HMD.
    /// </summary>
    public void FindHMD()
    {
        var system = OpenVR.System;
        if (system == null)
        {
            LogWarning("OpenVR System not found.");
            return;
        }

        if (_hmdIndex != OpenVR.k_unTrackedDeviceIndexInvalid &&
            system.GetTrackedDeviceClass(_hmdIndex) == ETrackedDeviceClass.HMD)
        {
            // Assume we as still connected to the HMD..
            return;
        }

        for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
        {
            if (system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.HMD) continue;
            _hmdIndex = i;
            break;
        }
        if (_hmdIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
        {
            Log("Found HMD ( Device: " + _hmdIndex + " )");
        }
    }

    /// <summary>
    /// Attempt to find both controllers.
    /// </summary>
    public void FindControllers()
    {
        var system = OpenVR.System;
        if (system == null)
        {
            LogWarning("OpenVR System not found.");
            return;
        }

        if (_leftIndex != OpenVR.k_unTrackedDeviceIndexInvalid && system.GetTrackedDeviceClass(_leftIndex) == ETrackedDeviceClass.Controller &&
            _rightIndex != OpenVR.k_unTrackedDeviceIndexInvalid && system.GetTrackedDeviceClass(_rightIndex) == ETrackedDeviceClass.Controller)
        {
            // Assume we are still connected to the controllers..
            return;
        }

        Log("Searching for Controllers..");
        _leftIndex = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
        _rightIndex = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);

        if (_leftIndex != OpenVR.k_unTrackedDeviceIndexInvalid && _rightIndex == OpenVR.k_unTrackedDeviceIndexInvalid)
        {
            Log("Found Controller ( Device: " + _leftIndex + " ): Left");
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (i == _leftIndex || system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.Controller)
                {
                    continue;
                }
                _rightIndex = i;
                Log("Found Controller ( Device: " + _rightIndex + " ): Right");
                break;
            }
        }
        else if (_leftIndex == OpenVR.k_unTrackedDeviceIndexInvalid && _rightIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
        {
            Log("Found Controller ( Device: " + _rightIndex + " ): Right");
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (i == _rightIndex || system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.Controller)
                {
                    continue;
                }
                _leftIndex = i;
                Log("Found Controller ( Device: " + _leftIndex + " ): Left");
                break;
            }
        }
        else if (_leftIndex == OpenVR.k_unTrackedDeviceIndexInvalid && _rightIndex == OpenVR.k_unTrackedDeviceIndexInvalid)
        {
            LogWarning("SteamVR Reports No Assigned Controllers..! Searching..");
            var foundUnassigned = 0;
            var slots = new uint[2];
            // Sort through all the devices until we find two controllers
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.Controller)
                {
                    continue; // This device isn't a controller, skip it
                }
                switch (system.GetControllerRoleForTrackedDeviceIndex(i))
                {
                    case ETrackedControllerRole.LeftHand:
                        Log("Found Controller ( Device: " + i + " ): Left");
                        _leftIndex = i;
                        break;
                    case ETrackedControllerRole.RightHand:
                        Log("Found Controller ( Device: " + i + " ): Right");
                        _rightIndex = i;
                        break;
                    case ETrackedControllerRole.Invalid:
                        Log("Found Controller ( Device: " + i + " ): Unassigned");
                        if (foundUnassigned <= 1)
                            slots[foundUnassigned++] = i;
                        break;
                }

                if (foundUnassigned == 2)
                {
                    break; // We have two controllers, stop searching
                }
            }
            switch (foundUnassigned)
            {
                case 2:
                    LogWarning("Found Two Unassigned Controllers! Randomly Assigning!");
                    _rightIndex = slots[0];
                    _leftIndex = slots[1];
                    break;
                case 1:
                    if (_leftIndex == OpenVR.k_unTrackedDeviceIndexInvalid &&
                       _rightIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
                    {
                        LogWarning("Only Found One Unassigned Controller, and Right was already assigned! Assigning To Left!");
                        _leftIndex = slots[0];
                    }
                    else
                    {
                        LogWarning("Only Found One Unassigned Controller! Assigning To Right!");
                        _rightIndex = slots[0];
                    }
                    break;
                case 0:
                    LogWarning("Couldn't Find Any Unassigned Controllers!");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// This is just used to quickly enable/disable Log messages.
    /// </summary>
    /// <param name="text"></param>
    void Log(string text)
    {
        //Debug.Log(text);
    }
    /// <summary>
    /// This is just used to quickly enable/disable LogWarning messages.
    /// </summary>
    /// <param name="text"></param>
    void LogWarning(string text)
    {
        //Debug.LogWarning(text);
    }
}