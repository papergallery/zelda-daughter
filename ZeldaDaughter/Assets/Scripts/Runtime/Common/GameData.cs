using UnityEngine;
using ZeldaDaughter.Core.Data;

namespace ZeldaDaughter
{
    /// <summary>
    /// data/*.json in the game: the same DataSet.Load as the server tests (ADR-0008). The editor copies data/ into
    /// Assets/Resources/Data (DataSync) — the copy is generated, never edited.
    /// </summary>
    public static class GameData
    {
        private static DataSet _current;

        public static DataSet Current => _current ?? (_current = Load());

        private static DataSet Load()
        {
            var data = DataSet.Load(name =>
            {
                var asset = Resources.Load<TextAsset>("Data/" + System.IO.Path.GetFileNameWithoutExtension(name));
                if (asset == null) throw new System.IO.FileNotFoundException("Resources/Data/" + name);
                return asset.text;
            });
            ZdLog.Info("Data", $"loaded items={data.Items.Count} recipes={data.FieldRecipes.Count}");
            return data;
        }

        /// <summary>Tests and the editor reload after data changed.</summary>
        public static void Reset() => _current = null;
    }
}
