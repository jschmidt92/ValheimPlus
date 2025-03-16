using System;
using UnityEngine;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ValheimPlus
{
    static class Helper
    {
		public static Character getPlayerCharacter(Player __instance)
		{
			return (Character)__instance;
		}

        /// <summary>
        /// A function to get the current player by network sender id, even in singleplayer
        /// </summary>
        public static Player getPlayerBySenderId(long id)
        {
            // A little more efficient than the Player.GetPlayer function but its miniscule.
            // This one also works in single player and requires no additonal work around.
            List<Player> allPlayers = Player.GetAllPlayers();
            foreach (Player player in allPlayers)
            {
                ZDOID zdoInfo = Helper.getPlayerCharacter(player).GetZDOID();
                if (zdoInfo != new ZDOID(0L, 0U))
                {
                    if (zdoInfo.UserID == id)
                        return player;
                }
            }
            return null;
        }

        public static bool IsSenderPlayerInRange(long senderId, float range)
        {
            var sendingPlayer = getPlayerBySenderId(senderId);
            var distance = Vector3.Distance(sendingPlayer.transform.position, Player.m_localPlayer.transform.position);
            return distance <= range;
        }

        public static float tFloat(this float value, int digits)
        {
            double mult = Math.Pow(10.0, digits);
            double result = Math.Truncate(mult * value) / mult;
            return (float)result;
        }

        // ReSharper disable once InconsistentNaming
        public static float applyModifierValue(float targetValue, float value) =>
            value <= -100f ? 0f : targetValue + (targetValue / 100.0f * value);

        // ReSharper disable once InconsistentNaming
        public static void applyModifierValueTo(ref float targetValue, float modifier)
        {
             targetValue = modifier <= -100f ? 0f : targetValue + (targetValue / 100.0f * modifier);
        }

        /// <summary>
        /// Calculate new value with chance mechanics.<br/><br/>
        /// On <c>targetValue = 1</c> and <c>value = 10</c> function will return "1 + (1 with 10% chance)"
        /// </summary>
        /// <param name="targetValue">Value to be modified</param>
        /// <param name="value">Modification coefficient in percentage</param>
        /// <returns>New value with chance mechanics</returns>
        public static int applyModifierValueWithChance(float targetValue, float value) {
            float realValue = applyModifierValue(targetValue, value);
            if (realValue == 0f)
                return 0;
            int guaranteedValue = (int)Math.Floor(realValue);

            // 1 - [0; 1) => (0; 1] -- to prevent additional drop on (realValue - guaranteedValue) being zero
            return guaranteedValue + ((realValue - guaranteedValue) > (1 - new System.Random().NextDouble()) ? 1 : 0);
        }

        public static Texture2D LoadPng(Stream fileStream)
        {
            Texture2D texture = null;

            if (fileStream != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    fileStream.CopyTo(memoryStream);

                    texture = new Texture2D(2, 2);
                    texture.LoadImage(memoryStream.ToArray()); //This will auto-resize the texture dimensions.
                }
            }

            return texture;
        }

        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }

                return sb.ToString();
            }
        }

        
        /// <summary>
        /// Resize child EffectArea's collision that matches the specified type(s).
        /// </summary>
        public static void ResizeChildEffectArea(MonoBehaviour parent, EffectArea.Type includedTypes, float newRadius)
        {
            if (parent != null)
            {
                EffectArea effectArea = parent.GetComponentInChildren<EffectArea>();
                if (effectArea != null)
                {
                    if ((effectArea.m_type & includedTypes) != 0)
                    {
                        SphereCollider collision = effectArea.GetComponent<SphereCollider>();
                        if (collision != null)
                        {
                            collision.radius = newRadius;
                        }
                    }
                }
            }
        }


        // Clamp value between min and max
        public static int Clamp(int value, int min, int max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        // Clamp value between min and max
        public static float Clamp(float value, float min, float max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        private const BindingFlags FieldBindingFlags =
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

        public static bool SetFieldIfFound(object obj, string field, object value)
        {
            var prop = obj.GetType().GetField(field, FieldBindingFlags);
            if (prop == null) return false;
            prop.SetValue(obj, value);
            return true;
        }
    }
}
