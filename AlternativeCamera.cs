﻿using UnityEngine;
using MelonLoader;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using System;

using Il2CppMegagon.Downhill.Cameras;

namespace AlternativeCameraMod
{
	/// <summary>
	/// Manages the alternative camera.<br />
	/// </summary>
	public class AlternativeCamera : MelonMod
	{
		private MelonPreferences_Category? mouseSettingsCat;
		private MelonPreferences_Category? cameraSettingsCat;
		public static AlternativeCamera? instance;
		private static KeyCode startKey;
		private static KeyCode primaryToggleKey;
		private static bool hasStartedOnce = false;
		private static bool modEnabled = true;
		private static bool forceDisable = false;

		private MelonPreferences_Entry<float> cfgSensitivityHorizontal;
		private MelonPreferences_Entry<float> cfgSensitivityVertical;
		private MelonPreferences_Entry<float> cfgSensitivityMultiplier;

		private MelonPreferences_Entry<float> cfgCameraCollisionPadding;
		private MelonPreferences_Entry<float> cfgZoomLerpOutSpeed;
		private MelonPreferences_Entry<float> cfgZoomLerpInSpeed;

		private MelonPreferences_Entry<bool> cfgInvertHorizontal;
		private MelonPreferences_Entry<bool> cfgDefaultCameraOnPause;
		private MelonPreferences_Entry<bool> cfgCameraAutoAlign;
		private MelonPreferences_Entry<float> cfgAutoAlignSpeed;

		private MelonPreferences_Entry<float> cfgZoomStepIncrement;
		private MelonPreferences_Entry<float> cfgStandardFoV;
		private MelonPreferences_Entry<float> cfgFirstPersonFoV;

		// Transforms and GameObjects
		//static readonly string defaultTargetName = "Bike(Clone)";
		/// <summary>The name of the gameobject that will act as the cameras target.</summary>
		private static string targetName = "Bike(Clone)";
		private static Transform playerBikeParent;
		private static Transform playerBikeObject;
		private static Transform camTransform;

		/// <summary>The main camera itself. Used to set the field of view.</summary>
		private static Camera mainCameraComponent;
		/// <summary>The ui camera. Used to toggle hud rendering.</summary>
		private static Camera uiCameraComponent;
		private static PlayCamera defaultCameraScript;

		// UI GameObjects
		private static GameObject ui_mainUIParent;
		private static GameObject ui_pauseMenuUI;
		private static GameObject ui_settingsUI;
		private static GameObject ui_controlsUI;
		private static GameObject ui_resultScreenUI;
		private static GameObject ui_highscoreStandaloneUI;
		private static GameObject ui_dailyChallengeStandaloneUI;
		private static GameObject ui_cutsceneUI;
		private static GameObject ui_cutsceneLocationUI;

		// Gameplay Settings
		private static Vector3 targetOffset = new Vector3(0f, 2.4f, 0f);
		private static LayerMask cameraCollisionLayers = LayerMask.GetMask("Ground") | LayerMask.GetMask("Obstacle") | LayerMask.GetMask("EnvironmentOther") | LayerMask.GetMask("Terrain") | LayerMask.GetMask("Lava");

		// Camera angle limits
		private static int xMinLimit = -80;
		private static int xMaxLimit = 80;

		// Active variables
		private static bool hasMenuOpen = true;
		//static bool invertCamVertical = false;	// WIP
		private static float wantedZoom = 8f;
		private static float targetZoomAmount;
		private static Quaternion rotation;

		/// <summary>The distance from the bike to any world-collision between it and the camera.</summary>
		private static float projectedDistance = 200f;
		/// <summary>Camera rotation around vertical y-axis (left-right)</summary>
		private static float rotHorizontal;
		/// <summary>Camera rotation around x-axis (ear-to-ear or up-down)</summary>
		private static float rotVertical;
		private static Vector3 dirToCam;

		public override void OnInitializeMelon()
		{
			mouseSettingsCat = MelonPreferences.CreateCategory("Mouse Settings");
			mouseSettingsCat.SetFilePath("UserData/AlternativeCameraSettings.cfg");

			cameraSettingsCat = MelonPreferences.CreateCategory("Camera Settings");
			cameraSettingsCat.SetFilePath("UserData/AlternativeCameraSettings.cfg");

			cfgSensitivityHorizontal = mouseSettingsCat.CreateEntry<float>("HorizontalSensitivity", 4f);
			cfgSensitivityVertical = mouseSettingsCat.CreateEntry<float>("VerticalSensitivity", 4f);
			cfgSensitivityMultiplier = mouseSettingsCat.CreateEntry<float>("SensitivityMultiplier", 0.040f);

			cfgInvertHorizontal = mouseSettingsCat.CreateEntry<bool>("InvertHorizontal", false);
			cfgZoomStepIncrement = mouseSettingsCat.CreateEntry<float>("ZoomStepIncrement", 0.20f, description:"How much one scroll zooms the camera.");

			cfgZoomLerpOutSpeed = cameraSettingsCat.CreateEntry<float>("ZoomOutLerpSpeed", 1.0f);
			cfgZoomLerpInSpeed = cameraSettingsCat.CreateEntry<float>("ZoomInLerpSpeed", 0.0880f);
			cfgCameraCollisionPadding = cameraSettingsCat.CreateEntry<float>("CameraCollisionPadding", 0.20f, description:"Distance the camera is pushed away from terrain.");

			cfgDefaultCameraOnPause = cameraSettingsCat.CreateEntry<bool>("DefaultCameraOnPause", true);
			cfgCameraAutoAlign = cameraSettingsCat.CreateEntry<bool>("CameraAutoAlign", false);
			cfgAutoAlignSpeed = cameraSettingsCat.CreateEntry<float>("AutoAlignSpeed", 1.80f, description:"How quickly the camera moves behind the player.");

			cfgStandardFoV = cameraSettingsCat.CreateEntry<float>("StandardFoV", 70f);
			cfgFirstPersonFoV = cameraSettingsCat.CreateEntry<float>("FirstPersonFoV", 98f);

			mouseSettingsCat.SaveToFile();
			cameraSettingsCat.SaveToFile();
		}
		public override void OnEarlyInitializeMelon()
		{
			instance = this;
			startKey = KeyCode.Alpha9;
			primaryToggleKey = KeyCode.Alpha0;
		}

		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			if (forceDisable)
			{
				return;
			}
			LoggerInstance.Msg($"Scene {sceneName} with build index {buildIndex} has been loaded!");
		}

		public override void OnLateUpdate()
		{
			if (forceDisable)
			{
				return;
			}

			//FIXME: Register the debug label
			//MelonEvents.OnGUI.Subscribe(DrawDebugText, 100);
			if (Input.GetKeyDown(startKey))
			{
				LoggerInstance.Msg("Starting alternative camera system!");

				// Assigning GO's
				GetTargetGameObjects();

				// Very hacky way of testing if paused
				GetUiObjects();

				// Apply some starting camera settings
				ApplyCameraSettings(5.4f, new Vector3(0f, 2.4f, 0f), 70f, 0.28f, "Bike(Clone)");

				Vector3 eulerAngles = camTransform.eulerAngles;
				rotHorizontal = eulerAngles.y;
				rotVertical = eulerAngles.x;

				hasStartedOnce = true;
			}

			// FIRST CHECKPOINT: Mod not ready
			if (!hasStartedOnce)
			{
				return;
			}

			if (Input.GetKeyDown(primaryToggleKey))
			{
				modEnabled = !modEnabled;
				if (!hasStartedOnce)
				{
					return;
				}

				if (modEnabled == false && defaultCameraScript.enabled == false)
				{
					defaultCameraScript.enabled = true;
					ApplyDefaultCameraSettings();
				}
				else
				{
					defaultCameraScript.enabled = false;
					ApplyCameraSettings(6f, new Vector3(0f, 2.4f, 0f), 70f, 0.28f, "Bike(Clone)");
				}
			}

			// SECOND CHECKPOINT: Mod not enabled
			if (modEnabled == false)
			{
				return;
			}


			// ==================== MAIN MOD METHODS ====================
			hasMenuOpen = GameMenuUiActive();

			// If the menu is open and default camera on pause is set don't run any functions
			if (hasMenuOpen && defaultCameraScript.enabled == false && cfgDefaultCameraOnPause.Value)
			{
				defaultCameraScript.enabled = true;
				return;
			}

			// All keybind handling
			HandleSettingsInputs();

			if (playerBikeObject == null)
			{
				return;
			}

			CameraLogic();
		}

		/// <summary>
		/// Handles the processing of the position and rotation of the camera.
		/// </summary>
		private void CameraLogic()
		{
			dirToCam = camTransform.position - playerBikeObject.TransformPoint(targetOffset);

			// Clamp distance at 0
			if (wantedZoom < 0.0f) {
				wantedZoom = 0.0f;
			}
			if (targetZoomAmount < 0.0f) {
				targetZoomAmount = 0.0f;
			}

			// Paused game check; only run when playing
			if (!hasMenuOpen)
			{
				// Double check that the default camera is disabled
				if (defaultCameraScript.enabled == true)
				{
					defaultCameraScript.enabled = false;
				}

				// Lock and hide the cursor
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;

				if (UnityEngine.Input.GetAxis("Mouse ScrollWheel") > 0f)
				{
					// Scrolling forward; zoom in
					wantedZoom -= cfgZoomStepIncrement.Value;
				}
				else if (UnityEngine.Input.GetAxis("Mouse ScrollWheel") < 0f)
				{
					// Scrolling backwards; zoom out
					wantedZoom += cfgZoomStepIncrement.Value;
				}

				// Horizontal mouse movement will make camera rotate around vertical y-axis
				// Vertical mouse movement will make camera rotate along x-axis (your ear-to-ear axis)
				rotHorizontal += UnityEngine.Input.GetAxisRaw("Mouse X") * cfgSensitivityHorizontal.Value * cfgSensitivityMultiplier.Value;
				rotVertical -= UnityEngine.Input.GetAxisRaw("Mouse Y") * cfgSensitivityVertical.Value * cfgSensitivityMultiplier.Value;
				rotVertical = ClampAngle(rotVertical, (float)xMinLimit, (float)xMaxLimit);  // Clamp the up-down rotation

				if (cfgInvertHorizontal.Value == true)
				{
					if (cfgCameraAutoAlign.Value == true)
					{
						// Lerp the horizontal rotation relative to the player
						rotHorizontal = Mathf.LerpAngle(rotHorizontal, -playerBikeParent.localRotation.eulerAngles.y, cfgAutoAlignSpeed.Value * Time.deltaTime);
						rotHorizontal = ClampAngle(rotHorizontal, -360, 360);
					}
					rotation = Quaternion.Euler(rotVertical, -rotHorizontal, 0f);
				}
				else
				{
					if (cfgCameraAutoAlign.Value == true)
					{
						// Lerp the horizontal rotation relative to the player
						rotHorizontal = Mathf.LerpAngle(rotHorizontal, playerBikeParent.localRotation.eulerAngles.y, cfgAutoAlignSpeed.Value * Time.deltaTime);
						rotHorizontal = ClampAngle(rotHorizontal, -360, 360);

						/*// Set the debug display values
						bikeRotationDebug = playerBikeParent.localRotation.eulerAngles.y;
						cameraRotHorizontalDebug = rotHorizontal;
						*/
					}
					rotation = Quaternion.Euler(rotVertical, rotHorizontal, 0f);
				}

				RaycastHit hitInfo;
				// Raycast from the target towards the camera
				if (Physics.Raycast(playerBikeObject.TransformPoint(targetOffset), dirToCam.normalized, out hitInfo, wantedZoom + 0.2f, cameraCollisionLayers))
				{
					projectedDistance = Vector3.Distance(hitInfo.point, playerBikeObject.TransformPoint(targetOffset));
				} else
				{
					projectedDistance = 900;
				}

				if (projectedDistance < wantedZoom)
				{
					// Desired camera distance is greater than the collision distance so zoom in to prevent clipping
					// b=bike, c=camera, *=collision
					// b-------*---c
					// b------c*
					float newTargetZoom = projectedDistance - cfgCameraCollisionPadding.Value;
					targetZoomAmount = Mathf.Lerp(targetZoomAmount, newTargetZoom, cfgZoomLerpInSpeed.Value);
				} else
				{
					// Zoom the camera back out to wanted distance over time
					targetZoomAmount = Mathf.Lerp(targetZoomAmount, wantedZoom, Time.deltaTime * cfgZoomLerpOutSpeed.Value);
				}


				Vector3 finalPosition = rotation * new Vector3(0f, 0f, -targetZoomAmount) + playerBikeObject.TransformPoint(targetOffset);

				// Apply values
				camTransform.position = finalPosition;
				camTransform.rotation = rotation;
			}
			else	// The menu is open; game is paused
			{
				// While paused show the cursor
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;

				// and optionally show the default camera
				if (defaultCameraScript.enabled == false && cfgDefaultCameraOnPause.Value)
				{
					defaultCameraScript.enabled = true;
				}
			}
		}

		/// <summary>
		/// Tries to clamp the angle to values between 360 and -360.
		/// </summary>
		private static float ClampAngle(float angle, float min, float max)
		{
			if (angle < -360f)
			{
				angle += 360f;
			}
			if (angle > 360f)
			{
				angle -= 360f;
			}
			return Mathf.Clamp(angle, min, max);
		}

		private static void GetTargetGameObjects()
		{
			playerBikeParent = GameObject.Find("Bike(Clone)").GetComponent<Transform>();
			playerBikeObject = GameObject.Find(targetName).GetComponent<Transform>();
			camTransform = GameObject.Find("PlayCamera(Clone)").GetComponent<Transform>();
			mainCameraComponent = camTransform.gameObject.GetComponent<Camera>();
			defaultCameraScript = camTransform.gameObject.GetComponent<PlayCamera>();
		}

		private static void GetUiObjects()
		{
			uiCameraComponent = GameObject.Find("UICam").GetComponent<Camera>();
			ui_mainUIParent = GameObject.Find("Wrapper");

			ui_pauseMenuUI = ui_mainUIParent.transform.Find("PauseScreen(Clone)").gameObject;
			ui_settingsUI = ui_mainUIParent.transform.Find("SettingsScreen(Clone)").gameObject;
			ui_controlsUI = ui_mainUIParent.transform.Find("TutorialScreen(Clone)").gameObject;
			ui_resultScreenUI = ui_mainUIParent.transform.Find("ResultScreen(Clone)").gameObject;
			ui_highscoreStandaloneUI = ui_mainUIParent.transform.Find("HighscoreStandalone(Clone)").gameObject;
			ui_dailyChallengeStandaloneUI = ui_mainUIParent.transform.Find("DailyChallengesStandalone(Clone)").gameObject;
			ui_cutsceneUI = ui_mainUIParent.transform.Find("CutsceneScreen(Clone)").gameObject;

			GameObject playscreenParentUI = ui_mainUIParent.transform.Find("PlayScreen(Clone)").gameObject;
			if (playscreenParentUI != null)
			{
				GameObject playscreenGroupUI = playscreenParentUI.transform.Find("PlayScreen_Group").gameObject;
				if (playscreenGroupUI != null)
				{
					ui_cutsceneLocationUI = playscreenGroupUI.transform.Find("HiddenLocationPanel").gameObject;
				}
			}

			Debug.Log("[AltCameraMod]Debug: Assigned UI GameObjects");
		}

		/// <summary>Resets the camera settings to default values.</summary>
		private void ApplyDefaultCameraSettings()
		{
			mainCameraComponent.fieldOfView = 34f;
			mainCameraComponent.nearClipPlane = 0.4f;   // FIXME: what is default nearClipPlane?
		}

		/// <summary>Allows applying multiple camera settings quickly.</summary>
		private void ApplyCameraSettings(float followDistance, Vector3 followTargetOffset, float cameraFov, float nearClipPlane, string followTargetName)
		{
			wantedZoom = followDistance;
			targetOffset = followTargetOffset;

			mainCameraComponent.fieldOfView = cameraFov;	// Default: 34
			mainCameraComponent.nearClipPlane = nearClipPlane;	// Default: ? FIXME:

			targetName = followTargetName;

			// Update references
			GetTargetGameObjects();

			LoggerInstance.Msg("Applied a camera preset!");
		}

		/// <summary>
		/// Checks if any menu gui is currently on screen.
		/// </summary>
		private static bool GameMenuUiActive()
		{
			if (ui_pauseMenuUI.active == true ||
				ui_settingsUI.active == true ||
				ui_controlsUI.active == true ||
				ui_resultScreenUI.active == true ||
				ui_highscoreStandaloneUI.active == true ||
				ui_dailyChallengeStandaloneUI.active == true ||
				ui_cutsceneUI.active == true
			)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Checks if the secret area gui is currently on screen.
		/// </summary>
		private static bool CutsceneSecretUiActive()
		{
			if (ui_cutsceneLocationUI != null && ui_cutsceneLocationUI.active == true)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Makes the camera move to directly behind the player.
		/// Useful for restarting at checkpoints.
		/// </summary>
		private void AlignViewWithBike()
		{
			Vector3 bikeRotation = playerBikeParent.localRotation.eulerAngles;
			if (cfgInvertHorizontal.Value == true)
			{
				rotHorizontal = -(bikeRotation.y);
			}
			else
			{
				rotHorizontal = bikeRotation.y;
			}
		}

		/// <summary>
		/// Handles all settings keybinds.
		/// </summary>
		private void HandleSettingsInputs()
		{
			if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad7))
			{
				// Toggle UI camera rendering
				uiCameraComponent.enabled = !uiCameraComponent.enabled;
				LoggerInstance.Msg("Toggled hud rendering ==> ["+ uiCameraComponent.enabled +"]");
			}

			if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad9))
			{
				// Find gameobjects again/update references on level load
				GetTargetGameObjects();
				GetUiObjects();
				AlignViewWithBike();
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
			{
				// On checkpoint restarts || On track restarts
				AlignViewWithBike();
			}

			if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
			{
				// Standard
				ApplyCameraSettings(7.2f, (new Vector3(0f, 2.4f, 0f)), cfgStandardFoV.Value, 0.2f, "Bike(Clone)");
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
			{
				// First Person
				ApplyCameraSettings(0f, (new Vector3(0.0f, 0.3f, 0f)), cfgFirstPersonFoV.Value, 0.6f, "neck_BindJNT");

				// Navigate to bike mesh renderer to prevent it from vanishing in first person
				SkinnedMeshRenderer bikeMeshRenderer = playerBikeParent.GetChild(7).transform.GetChild(1).gameObject.GetComponent<SkinnedMeshRenderer>();
				bikeMeshRenderer.updateWhenOffscreen = true;
			}

			// Mouse inverting
			if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad3))
			{
				cfgInvertHorizontal.Value = !cfgInvertHorizontal.Value;
				LoggerInstance.Msg("Toggled invert camera horizontal ==> ["+ cfgInvertHorizontal.Value +"]");
			}

			// Camera auto align
			if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad4))
			{
				cfgCameraAutoAlign.Value = !cfgCameraAutoAlign.Value;
				LoggerInstance.Msg("Toggled auto-align ==> ["+ cfgCameraAutoAlign.Value +"]");
			}
		}

		/*public static void DrawDebugText()
		{
			String bikeRotText = "<b><color=white><size=20>Bike: "+ bikeRotationDebug +"</size></color></b>";
			String camRotText = "<b><color=white><size=20>Camera: "+ cameraRotHorizontalDebug +"</size></color></b>";
			GUI.Label(new Rect(20, 20, 200, 80), bikeRotText);
			GUI.Label(new Rect(20, 120, 200, 80), camRotText);
		}
		*/

		public override void OnDeinitializeMelon()
		{
			// In case the melon gets unregistered
			forceDisable = true;
		}
	}
}