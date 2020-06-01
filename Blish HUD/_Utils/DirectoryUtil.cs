﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blish_HUD {

    /// <summary>
    /// Provides a way to get the root application save directory and to easily make new folders within it.
    /// </summary>
    public static class DirectoryUtil {

        private const string ADDON_DIR = @"GUILD WARS 2\addons\blishhud";

        private const string SCREENS_DIR = @"GUILD WARS 2\Screens";

        private const string MUSIC_DIR = @"GUILD WARS 2\music";

        /// <summary>
        /// The current root application save path used for saving settings, letting modules save data, etc.
        /// By default it is found in "Documents\GUILD WARS 2\addons\blishhud."
        /// </summary>
        public static string BasePath { get; }
        /// <summary>
        /// The path used by the game client for saving screenshots made in the game (usually using the print screen key).
        /// By default it is found in "Documents\GUILD WARS 2\Screens."
        /// </summary>
        public static string ScreensPath { get; }
        /// <summary>
        /// The path used by the game client for loading custom music playlists in a context-sensitive manner during gameplay.
        /// By default it is found in "Documents\GUILD WARS 2\music."
        /// </summary>
        public static string MusicPath { get; }

        static DirectoryUtil() {
            // Prepare user documents directory
            BasePath = ApplicationSettings.Instance.UserSettingsPath
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                                                               Environment.SpecialFolderOption.DoNotVerify),
                                     ADDON_DIR);

            ScreensPath = ApplicationSettings.Instance.UserSettingsPath
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                                                                 Environment.SpecialFolderOption.DoNotVerify),
                                       SCREENS_DIR);

            MusicPath = ApplicationSettings.Instance.UserSettingsPath
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                                                                 Environment.SpecialFolderOption.DoNotVerify),
                                       MUSIC_DIR);

            Directory.CreateDirectory(BasePath);
        }
        public static string RegisterDirectory(string directory) => Directory.CreateDirectory(Path.Combine(BasePath, directory)).FullName;

    }

}
