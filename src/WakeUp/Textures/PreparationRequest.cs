// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Xml;

namespace WakeUp;

// The standalone window writes only this Wake-Up-owned request. The next
// ordinary game launch applies it to Wake-Up settings and consumes it once.
internal static class PreparationRequest
{
    internal static void Apply(string saveRoot, WakeUpSettings settings)
    {
        string path=Path.Combine(saveRoot,"WakeUp","PreparedTextures","request.xml");
        if(!File.Exists(path)) return;
        OwnedCacheStore.RejectLinkedPath(path);
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(input.Length<1 || input.Length>4096) throw new InvalidDataException("preparation-request-size");
            using var reader=XmlReader.Create(input,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096});
            var xml=new XmlDocument {XmlResolver=null};xml.Load(reader);
            var root=xml.DocumentElement;
            if(root==null || root.Name!="WakeUpPreparationRequest" || root.GetAttribute("version")!="1")
                throw new InvalidDataException("preparation-request-version");
            foreach(XmlNode node in root.ChildNodes)
                if(node is XmlElement && node.Name!="enabled" && node.Name!="sharedCacheMiB" && node.Name!="bypassOnce" && node.Name!="clearOnce")
                    throw new InvalidDataException("preparation-request-field");
            bool Flag(string name)
            {
                string? text=root.SelectSingleNode(name)?.InnerText;
                if(text==null) return false;
                if(!bool.TryParse(text,out bool value) || root.SelectNodes(name)!.Count!=1) throw new InvalidDataException("preparation-request-"+name);
                return value;
            }
            bool enable=Flag("enabled"),bypass=Flag("bypassOnce"),clear=Flag("clearOnce");
            if(bypass && clear) throw new InvalidDataException("conflicting-preparation-maintenance");
            string? ceiling=root.SelectSingleNode("sharedCacheMiB")?.InnerText;
            int budget=settings.SharedCacheMiB;
            if(ceiling!=null && (!int.TryParse(ceiling,out budget) || budget<1 || budget>65536 || root.SelectNodes("sharedCacheMiB")!.Count!=1))
                throw new InvalidDataException("preparation-request-budget");
            if(enable) settings.PreparedTextures=true;
            settings.SharedCacheMiB=budget;
            if(bypass) settings.CacheMaintenanceNextLaunch="bypass";
            if(clear) settings.CacheMaintenanceNextLaunch="clear";
            settings.Write();
        }
        File.Delete(path);
    }
}
