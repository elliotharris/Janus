﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Janus.Properties;
using JanusSharedLib;

namespace Janus
{
    public class DataStore
    {
        public DataStore(string pathOverride = null)
        {
            DataLocation   = pathOverride ?? Path.Combine(AppData, Assembly.GetEntryAssembly().GetName().Name);
            DataLocation   = Path.GetFullPath(DataLocation);
            LoaderLocation = Path.Combine(AssemblyDirectory, LoaderName);
            _storeName      = Path.Combine(DataLocation, "watchdata");
            Console.WriteLine(_storeName);
        }

        public static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        /// <summary>
        /// Directory of the data storage file for this DataStore object.
        /// </summary>
        public readonly string DataLocation;

        /// <summary>
        /// Full file path of the data file (DataLocation\watchdata)
        /// </summary>
        private readonly string _storeName;

        /// <summary>
        /// Maps Version numbers to data loaders.
        /// Enables backwards compatibility.
        /// </summary>
        public readonly Dictionary<long, IDataStorageFormat> DataLoaders = new Dictionary<long, IDataStorageFormat>();

        private const string LoaderName = "StorageFormats.dll";

        /// <summary>
        /// Location of the exe (not current directory!)
        /// </summary>
        public static string AssemblyDirectory
        {
            get
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                Logging.WriteLine(path);
                return Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        /// Full location of the DLL that contains storage formats that will be loaded.
        /// </summary>
        public readonly string LoaderLocation;

        /// <summary>
        /// Current StorageFormat version.
        /// Used for maintaining possible backwards compatibilty.
        /// </summary>
        public static readonly long Version = 0x5;

        /// <summary>
        /// First bytes in the store file.
        /// Should NEVER change.
        /// </summary>
        private readonly byte[] _headerBytes = { 0x04, (byte)'j', (byte)'w', 0x23};


        /// <summary>
        /// Loads in StorageFormats.dll and all it's IDataStorageFormat classes.
        /// (Classes must be marked with StorageFormat attribute)
        /// </summary>
        public void Initialise()
        {
            Logging.WriteLine(AssemblyDirectory);
            Logging.WriteLine(LoaderLocation);
            var a = Assembly.UnsafeLoadFrom(LoaderLocation);
            foreach (var t in a.GetTypes())
            {
                foreach (var attr in t.GetCustomAttributes(true))
                {
                    if (attr is StorageFormatAttribute formatAttr)
                    {
                        var c = (IDataStorageFormat) Activator.CreateInstance(t);
                        DataLoaders.Add(formatAttr.VersionNumber, c);
                    }
                }
            }
        }

        /// <summary>
        /// Saves Watcher data + any DataProvider data
        /// </summary>
        /// <param name="data">Data to be saved to disk</param>
        public void Store(JanusData data)
        {
            if (!Directory.Exists(DataLocation))
            {
                Directory.CreateDirectory(DataLocation);
            }

            using (var fs = File.Create(_storeName))
            {
                var writer = new BinaryWriter(fs);
                writer.Write(_headerBytes);
                writer.Write(Version);
                if (DataLoaders.TryGetValue(Version, out var format))
                {
                    format.Save(writer, data);
                }
                // To get here the user's install must be corrupted.

                // TODO: Add user dialog prompting a reinstall here
                // TODO: Check this on startup so we don't only error on save.
                // TODO: Automatically pull correct IDataStore from server?
                //        Could apply to loading too.
            }
        }

        /// <summary>
        /// Loads in data from [StoreName].
        /// </summary>
        /// <returns>Watchers and DataProvider data</returns>
        public JanusData Load()
        {
            if (!Directory.Exists(DataLocation))
            {
                Directory.CreateDirectory(DataLocation);
            }

            if (!File.Exists(_storeName))
            {
                Logging.WriteLine("No previous data found.");
                return new JanusData();
            }
            Logging.WriteLine($"Loading from: {_storeName}");
            using (var fs = File.OpenRead(_storeName))
            {
                var reader = new BinaryReader(fs);
                var header = reader.ReadBytes(4);
                if (!header.SequenceEqual(_headerBytes))
                {
                    InvalidDataStore("Invalid header", fs);
                    return new JanusData();
                }
                var version = reader.ReadInt64();
                Logging.WriteLine($"DataStore Version: {version}");
                if (DataLoaders.TryGetValue(version, out var format))
                {
                    try
                    {
                        return format.Read(reader);
                    }
                    catch (IOException)
                    {
                        return format.Read(reader);
                    }
                    catch (Exception e)
                    {
                        InvalidDataStore(e.Message, fs);
                    }
                }
                InvalidDataStore("Unsupported format", fs);
                return new JanusData();
            }
        }

        /// <summary>
        /// Called when an invalid data store is tried to be loaded.
        /// Prints an error and deletes the invalid store.
        /// </summary>
        /// <param name="fs">Open File Stream</param>
        /// <param name="message">Error reason</param>
        private void InvalidDataStore(string message, Stream fs)
        {
            Logging.WriteLine(Resources.Invalid_DataStore, message);
            fs?.Close();
            var retries = 5;
            while (retries > 0)
            {
                try
                {
                    File.Copy(_storeName, _storeName + $"--invalid-{DateTime.Now}.old");
                    File.Delete(_storeName);
                    break;
                }
                catch (Exception e)
                {
                    Logging.WriteLine($"Failed to rename invalid data store, Retries - {retries}. Message - {e.Message}");
                }

                retries--;
            }
        }
    }
}
