﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.ShortcutManagement;

namespace Gemserk
{
	public class SelectionHistoryWindow : EditorWindow {

		public static bool shouldReloadPreferences = true;
		
	    [MenuItem ("Window/Gemserk/Selection History Old")]
		static void Init () {
			// Get existing open window or if none, make a new one:
			var window = EditorWindow.GetWindow<SelectionHistoryWindow> ();
			window.titleContent.text = "OldWindow/History";
			window.minSize = new Vector2(300, 200);
			window.Show();
		}


		public GUISkin windowSkin;

		private static SelectionHistory selectionHistory
		{
			get { return SelectionHistoryContext.SelectionHistory; }
		}

		void OnEnable()
		{
			automaticRemoveDeleted = EditorPrefs.GetBool (SelectionHistoryWindowConstants.HistoryAutomaticRemoveDeletedPrefKey, true);

			// selectionHistory = EditorTemporaryMemory.Instance.selectionHistory;
			selectionHistory.HistorySize = EditorPrefs.GetInt (SelectionHistoryWindowConstants.HistorySizePrefKey, 10);

			selectionHistory.cleared += Repaint;

			Selection.selectionChanged += delegate {

				if (selectionHistory.IsSelected(selectionHistory.GetHistoryCount() - 1)) {
					_historyScrollPosition.y = float.MaxValue;
				}

				Repaint();
			};
		}

		void UpdateSelection(Object obj)
		{
		    selectionHistory.SetSelection(obj);
            Selection.activeObject = obj;
            // Selection.activeObject = selectionHistory.UpdateSelection(currentIndex);
		}

	    private Vector2 _favoritesScrollPosition;
		private Vector2 _historyScrollPosition;

		bool automaticRemoveDeleted;
		bool allowDuplicatedEntries;

	    bool showHierarchyViewObjects;
	    bool showProjectViewObjects;

        void OnGUI () {

			if (shouldReloadPreferences) {
				selectionHistory.HistorySize = EditorPrefs.GetInt (SelectionHistoryWindowConstants.HistorySizePrefKey, 10);
				automaticRemoveDeleted = EditorPrefs.GetBool (SelectionHistoryWindowConstants.HistoryAutomaticRemoveDeletedPrefKey, true);
				allowDuplicatedEntries = EditorPrefs.GetBool (SelectionHistoryWindowConstants.HistoryAllowDuplicatedEntriesPrefKey, false);

			    showHierarchyViewObjects = EditorPrefs.GetBool(SelectionHistoryWindowConstants.HistoryShowHierarchyObjectsPrefKey, true);
			    showProjectViewObjects = EditorPrefs.GetBool(SelectionHistoryWindowConstants.HistoryShowProjectViewObjectsPrefKey, true);

                shouldReloadPreferences = false;
			}

			if (automaticRemoveDeleted)
				selectionHistory.ClearDeleted ();

			if (!allowDuplicatedEntries)
				selectionHistory.RemoveDuplicated ();

            var favoritesEnabled = EditorPrefs.GetBool(SelectionHistoryWindowConstants.HistoryFavoritesPrefKey, true);
            if (favoritesEnabled && selectionHistory.Favorites.Count > 0)
            {
                _favoritesScrollPosition = EditorGUILayout.BeginScrollView(_favoritesScrollPosition);
                DrawFavorites();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Separator();
            }
        
            bool changedBefore = GUI.changed;

			_historyScrollPosition = EditorGUILayout.BeginScrollView(_historyScrollPosition);

			bool changedAfter = GUI.changed;

			if (!changedBefore && changedAfter) {
				Debug.Log ("changed");
			}

			DrawHistory();

			EditorGUILayout.EndScrollView();

			if (GUILayout.Button("Clear")) {
				selectionHistory.Clear();
				Repaint();
			}

			if (!automaticRemoveDeleted) {
				if (GUILayout.Button ("Remove Deleted")) {
					selectionHistory.ClearDeleted ();
					Repaint ();
				}
			} 

			if (allowDuplicatedEntries) {
				if (GUILayout.Button ("Remove Duplciated")) {
					selectionHistory.RemoveDuplicated ();
					Repaint ();
				}
			} 

			DrawSettingsButton ();
		}

		void DrawSettingsButton()
		{
			if (GUILayout.Button ("Preferences")) {
				SettingsService.OpenUserPreferences(SelectionHistoryPreferences.PreferencesPath);
			}
		}
			
		[MenuItem("Window/Gemserk/Previous selection %#,")]
		public static void PreviousSelection()
		{
			selectionHistory.Previous ();
			Selection.activeObject = selectionHistory.GetSelection ();
		}

		[MenuItem("Window/Gemserk/Next selection %#.")]
		public static void NextSelection()
		{
			selectionHistory.Next();
			Selection.activeObject = selectionHistory.GetSelection ();
		}

	    void DrawElement(Object obj, int i, Color originalColor)
	    {
	        var buttonStyle = windowSkin.GetStyle("SelectionButton");
            var nonSelectedColor = originalColor;

            if (!EditorUtility.IsPersistent(obj))
            {
                if (!showHierarchyViewObjects)
                    return;
                nonSelectedColor = SelectionHistoryWindowConstants.hierarchyElementColor;
            }
            else
            {
                if (!showProjectViewObjects)
                    return;
            }

            if (selectionHistory.IsSelected(obj))
            {
                GUI.contentColor = SelectionHistoryWindowConstants.selectedElementColor;
            }
            else
            {
                GUI.contentColor = nonSelectedColor;
            }

            var rect = EditorGUILayout.BeginHorizontal();

            if (obj == null)
            {
                GUILayout.Label("Deleted", buttonStyle);
            }
            else
            {
                var icon = AssetPreview.GetMiniThumbnail(obj);

                GUIContent content = new GUIContent();

                content.image = icon;
                content.text = obj.name;

                // chnanged to label to be able to handle events for drag
                GUILayout.Label(content, buttonStyle);

                GUI.contentColor = originalColor;

                if (GUILayout.Button("Ping", windowSkin.button))
                {
                    EditorGUIUtility.PingObject(obj);
                }

                var favoritesEnabled = EditorPrefs.GetBool(SelectionHistoryWindowConstants.HistoryFavoritesPrefKey, true);

                if (favoritesEnabled)
                {
                    var pinString = "Pin";
                    var isFavorite = selectionHistory.IsFavorite(obj);

                    if (isFavorite)
                    {
                        pinString = "Unpin";
                    }

                    if (GUILayout.Button(pinString, windowSkin.button))
                    {
                        selectionHistory.ToggleFavorite(obj);
                        Repaint();
                    }
                }

            }

            EditorGUILayout.EndHorizontal();

            ButtonLogic(rect, obj);
        }

	    void DrawFavorites()
	    {
	        var originalColor = GUI.contentColor;

	        var favorites = selectionHistory.Favorites;

	        var buttonStyle = windowSkin.GetStyle("SelectionButton");

	        for (int i = 0; i < favorites.Count; i++)
	        {
	            var favorite = favorites[i];
                DrawElement(favorite, i, originalColor);
	        }

	        GUI.contentColor = originalColor;
        }

		void DrawHistory()
		{
			var originalColor = GUI.contentColor;

			var history = selectionHistory.History;

			var buttonStyle = windowSkin.GetStyle("SelectionButton");

		    var favoritesEnabled = EditorPrefs.GetBool(SelectionHistoryWindowConstants.HistoryFavoritesPrefKey, true);

            for (int i = 0; i < history.Count; i++) {
				var historyElement = history [i];
                if (selectionHistory.IsFavorite(historyElement) && favoritesEnabled)
                    continue;
			    DrawElement(historyElement, i, originalColor);
            }

			GUI.contentColor = originalColor;
		}

		void ButtonLogic(Rect rect, Object currentObject)
		{
			var currentEvent = Event.current;

			if (currentEvent == null)
				return;

			if (!rect.Contains (currentEvent.mousePosition))
				return;
			
//			Debug.Log (string.Format("event:{0}", currentEvent.ToString()));

			var eventType = currentEvent.type;

			if (eventType == EventType.MouseDrag && currentEvent.button == 0) {

				if (currentObject != null) {
					DragAndDrop.PrepareStartDrag ();

					DragAndDrop.StartDrag (currentObject.name);

					DragAndDrop.objectReferences = new Object[] { currentObject };

//					if (ProjectWindowUtil.IsFolder(currentObject.GetInstanceID())) {

					// fixed to use IsPersistent to work with all assets with paths.
					if (EditorUtility.IsPersistent(currentObject)) {

						// added DragAndDrop.path in case we are dragging a folder.

						DragAndDrop.paths = new string[] {
							AssetDatabase.GetAssetPath(currentObject)
						};

						// previous test with setting generic data by looking at
						// decompiled Unity code.

						// DragAndDrop.SetGenericData ("IsFolder", "isFolder");
					}
				}

				Event.current.Use ();

			} else if (eventType == EventType.MouseUp) {

				if (currentObject != null) {
					if (Event.current.button == 0) {
						UpdateSelection (currentObject);
					} else {
						EditorGUIUtility.PingObject (currentObject);
					}
				}

				Event.current.Use ();
			}

		}

	}
}