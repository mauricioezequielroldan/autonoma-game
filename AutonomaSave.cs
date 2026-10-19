using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Autonoma.Core
{
    [Serializable]
    public sealed class AutonomaSaveData
    {
        public int SchemaVersion = 1;
        public string BuildVersion = "1.0";
        public long SaveUnixSeconds;
        public long GameMinutes;
        public string WorldSeed = "default";
        public CharacterSaveData Character = new CharacterSaveData();
        public WorldSaveData World = new WorldSaveData();
        public List<string> EventMemory = new List<string>();
    }

    [Serializable]
    public sealed class CharacterSaveData
    {
        public string CharacterId = "character_001";
        public float Satiety = 75;
        public float Energy = 80;
        public float Fear = 10;
        public float Mood = 70;
        public float Bond = 50;
        public float Faith = 50;
        public float Wetness;
        public float ColdExposure;
        public float[] Traits = new float[5];
        public List<SkillSaveData> Skills = new List<SkillSaveData>();
        public List<MasterSignatureSaveData> Signatures = new List<MasterSignatureSaveData>();
    }

    [Serializable]
    public sealed class SkillSaveData
    {
        public string SkillId;
        public float Knowledge;
        public long LastPracticeGameMinute;
        public long LastDecayGameMinute;
        public bool Dominated;
        public List<string> EpisodeTokens = new List<string>();
    }

    [Serializable]
    public sealed class MasterSignatureSaveData
    {
        public string SkillId;
        public string QuirkId;
        public float Strength;
        public long LastObservedGameMinute;
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        public string WeatherKind = "Clear";
        public float WeatherIntensity;
        public float Wetness;
        public float FireLifetimeSeconds = 25;
        public int CampLevel;
        public string ShelterKind = "None";
        public List<string> Inventory = new List<string>();
        public List<string> WorldFlags = new List<string>();
    }

    public interface ISaveCodec
    {
        string Encode(AutonomaSaveData data);
        AutonomaSaveData Decode(string payload);
    }

    /// <summary>
    /// Save coordinator. Supply a JSON codec from your runtime (System.Text.Json,
    /// Newtonsoft.Json, Unity JsonUtility, or a custom serializer). This keeps the
    /// simulation independent of a specific engine and avoids hidden serializer assumptions.
    /// </summary>
    public sealed class AutonomaSave
    {
        private readonly ISaveCodec _codec;
        public AutonomaSave(ISaveCodec codec) { _codec = codec ?? throw new ArgumentNullException(nameof(codec)); }

        public void SaveToPath(string path, AutonomaSaveData data)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.SaveUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string payload = _codec.Encode(data);
            string envelope = BuildEnvelope(payload);
            string temp = path + ".tmp";
            string backup = path + ".bak";
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(dir);
            File.WriteAllText(temp, envelope, Encoding.UTF8);
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, backup, true); }
                catch (PlatformNotSupportedException) { File.Copy(path, backup, true); File.Delete(path); File.Move(temp, path); }
                catch (IOException) { File.Copy(path, backup, true); File.Delete(path); File.Move(temp, path); }
            }
            else File.Move(temp, path);
        }

        public AutonomaSaveData LoadFromPath(string path)
        {
            if (!File.Exists(path)) return null;
            try { return ReadAndValidate(File.ReadAllText(path, Encoding.UTF8)); }
            catch
            {
                string backup = path + ".bak";
                if (File.Exists(backup)) return ReadAndValidate(File.ReadAllText(backup, Encoding.UTF8));
                throw;
            }
        }

        private AutonomaSaveData ReadAndValidate(string envelope)
        {
            string[] parts = envelope.Split(new[] { '\n' }, 3);
            if (parts.Length != 3 || parts[0] != "AUTONOMA_SAVE_V1")
                throw new InvalidDataException("Unknown or corrupt save envelope.");
            string expected = parts[1];
            string payload = parts[2];
            string actual = Sha256(payload);
            if (!FixedTimeEquals(expected, actual)) throw new InvalidDataException("Save checksum mismatch.");
            AutonomaSaveData data = _codec.Decode(payload);
            if (data == null) throw new InvalidDataException("Save payload decoded to null.");
            Migrate(data);
            return data;
        }

        private static string BuildEnvelope(string payload) => "AUTONOMA_SAVE_V1\n" + Sha256(payload) + "\n" + payload;

        private static void Migrate(AutonomaSaveData data)
        {
            if (data.SchemaVersion < 1) data.SchemaVersion = 1;
            if (data.Character == null) data.Character = new CharacterSaveData();
            if (data.World == null) data.World = new WorldSaveData();
            if (data.EventMemory == null) data.EventMemory = new List<string>();
            if (data.Character.Skills == null) data.Character.Skills = new List<SkillSaveData>();
            if (data.Character.Signatures == null) data.Character.Signatures = new List<MasterSignatureSaveData>();
            data.SchemaVersion = 1;
        }

        private static string Sha256(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
