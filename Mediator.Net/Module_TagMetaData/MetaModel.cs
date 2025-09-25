// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System;

namespace Ifak.Fast.Mediator.TagMetaData;

[XmlRoot(Namespace = "TAG", ElementName = "MetaModel")]
public sealed class MetaModel
{
    public List<What> Whats { get; set; } = [];
    public List<UnitGroup> UnitGroups { get; set; } = [];
    public List<Unit> Units { get; set; } = [];
    public List<Category> Categories { get; set; } = [];

    public void Validate()
    {
        // unique IDs for What:
        HashSet<string> whatIds = [];
        foreach (var what in Whats)
        {
            if (string.IsNullOrEmpty(what.ID))
                throw new Exception("What element must have an 'id' attribute.");
            if (!whatIds.Add(what.ID))
                throw new Exception($"Duplicate 'id' found in What elements: {what.ID}");
        }

        // unique IDs for Units:
        HashSet<string> unitIds = [];
        foreach (var unit in Units)
        {
            if (string.IsNullOrEmpty(unit.ID))
                throw new Exception("Unit element must have an 'id' attribute.");
            if (!unitIds.Add(unit.ID))
                throw new Exception($"Duplicate 'id' found in Unit elements: {unit.ID}");
        }

        // RefUnit is set and refers to a valid unit:
        foreach (var what in Whats)
        {
            if (string.IsNullOrEmpty(what.RefUnit))
                throw new Exception($"What element with id '{what.ID}' must have a 'unit' attribute.");
            if (!Units.Exists(u => u.ID == what.RefUnit))
                throw new Exception($"What element with id '{what.ID}' refers to an unknown unit: {what.RefUnit}");

            if (string.IsNullOrEmpty(what.UnitGroup))
                throw new Exception($"What element with id '{what.ID}' must have a 'unitGroup' attribute.");
            if (!UnitGroups.Exists(ug => ug.ID == what.UnitGroup))
                throw new Exception($"What element with id '{what.ID}' refers to an unknown UnitGroup: {what.UnitGroup}");

            if (string.IsNullOrEmpty(what.Category))
                throw new Exception($"What element with id '{what.ID}' must have a 'category' attribute.");
            if (!Categories.Exists(c => c.ID == what.Category))
                throw new Exception($"What element with id '{what.ID}' refers to an unknown Category: {what.Category}");

            if (Units.Find(u => u.ID == what.RefUnit)!.UnitGroup != what.UnitGroup) 
                throw new Exception($"What element with id '{what.ID}' refers to a unit '{what.RefUnit}' that does not match its UnitGroup '{what.UnitGroup}'.");
        }

        // UnitGroup, Name combination must be unique:
        HashSet<(string, string)> unitGroupNamePairs = [];
        foreach (var what in Whats) {
            if (string.IsNullOrEmpty(what.Name))
                throw new Exception($"What element with id '{what.ID}' must have a 'name' attribute.");
            var pair = (what.UnitGroup, what.Name);
            if (!unitGroupNamePairs.Add(pair))
                throw new Exception($"Duplicate combination of UnitGroup and Name found in What elements: {what.UnitGroup}, {what.Name}");
        }

        // Unique IDs for UnitGroups:
        HashSet<string> unitGroupIds = [];
        foreach (var unitGroup in UnitGroups)
        {
            if (string.IsNullOrEmpty(unitGroup.ID))
                throw new Exception("UnitGroup element must have an 'id' attribute.");
            if (!unitGroupIds.Add(unitGroup.ID))
                throw new Exception($"Duplicate 'id' found in UnitGroup elements: {unitGroup.ID}");
        }

        // Unique IDs for Categories:
        HashSet<string> categoryIds = [];
        foreach (var category in Categories)
        {
            if (string.IsNullOrEmpty(category.ID))
                throw new Exception("Category element must have an 'id' attribute.");
            if (!categoryIds.Add(category.ID))
                throw new Exception($"Duplicate 'id' found in Category elements: {category.ID}");
        }
    }
}

public sealed class What 
{
    [XmlAttribute("id")]
    public string ID { get; set; } = "";

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    [XmlAttribute("short")]
    public string ShortName { get; set; } = "";

    [XmlAttribute("unitGroup")]
    public string UnitGroup { get; set; } = "";

    [XmlAttribute("category")]
    public string Category { get; set; } = ""; // e.g. Liquid, Gas, Nitrogen, Phosphorus

    [XmlAttribute("unit")]
    public string RefUnit { get; set; } = "";
}

public sealed class Unit
{
    [XmlAttribute("id")]
    public string ID { get; set; } = "";

    [XmlAttribute("unitGroup")]
    public string UnitGroup { get; set; } = "";

    [XmlAttribute("si")]
    public bool IsSI { get; set; } = true;

    [XmlAttribute("factor")]
    public double Factor { get; set; } = 1.0;

    [XmlAttribute("offset")]
    public double Offset { get; set; } = 0.0;

    public static double ConvertUnits(Unit fromUnit, Unit toUnit, double value) {
        if (fromUnit.UnitGroup != toUnit.UnitGroup)
            throw new System.ArgumentException("Cannot convert between different unit groups.");
        // Convert to ref unit:
        double valueInRefUnit = value * fromUnit.Factor + fromUnit.Offset;
        // Convert from ref unit to target unit:
        double valueInTargetUnit = (valueInRefUnit - toUnit.Offset) / toUnit.Factor;
        return valueInTargetUnit;
    }
}


public sealed class UnitGroup
{
    [XmlAttribute("id")]
    public string ID { get; set; } = "";
}

public sealed class Category
{
    [XmlAttribute("id")]
    public string ID { get; set; } = "";
}
