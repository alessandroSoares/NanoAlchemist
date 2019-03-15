using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml;

namespace NanoAlchemist.Display.Files
{
    public class CreationalWorkstationFile
    {
        private ZipArchive _sliceFile;
        private XmlDocument _manifest;
        private Dictionary<int, string> _layers;

        public event EventHandler OnFileLoaded;
        public event EventHandler<double> OnLayerProgressChanged;

        public int LayerCount { get; private set; } = 0;

        public async void LoadSliceFile(Stream streamFile)
        {
            try
            {
                await Task.Run(() =>
                {
                    _manifest = new XmlDocument();
                    _layers = new Dictionary<int, string>();
                    _sliceFile = new ZipArchive(streamFile);
                    var manifestEntry = _sliceFile.GetEntry("manifest.xml");
                    var stream = manifestEntry.Open();
                    var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                    _manifest.Load(memStream);
                    LoadLayers();
                });

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void LoadLayers()
        {
            if (_manifest == null)
                return;

            var slicesNode = _manifest["manifest"]["Slices"];
            LayerCount = slicesNode.ChildNodes.Count;

            for (int i = 0; i < LayerCount; i++)
            {
                var slice = slicesNode.ChildNodes[i];
                var sliceName = slice.ChildNodes[0];
                var entryName = sliceName.InnerText;
                _layers.Add(i, entryName);

                OnLayerProgressChanged?.Invoke(this, (double)i / LayerCount);
            }

            OnFileLoaded?.Invoke(this, new EventArgs());
        }

        public async Task<MemoryStream> GetLayerStream(int layer)
        {
            if (layer > LayerCount || layer == 0)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    var entryName = _layers[layer - 1];
                    var entry = _sliceFile.GetEntry(entryName);
                    var stream = entry.Open();
                    var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Position = 0;
                    return memStream;
                }
                catch
                {
                    return new MemoryStream();
                }
            });

        }

        public void Dispose()
        {
            _sliceFile.Dispose();
        }

    }
}
