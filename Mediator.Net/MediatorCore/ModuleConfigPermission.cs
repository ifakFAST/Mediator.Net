// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ifak.Fast.Mediator;

public sealed class ModuleConfigPermission {

    private readonly Dictionary<string, RoleInfo> allowedConfigChangesPerRole = new();
    private readonly string moduleID;

    private sealed class RoleInfo {
        public bool AllowAllConfigChanges = false;
        public HashSet<MemberRef> ChangeableMembers = new();
    }

    public ModuleConfigPermission(string moduleID) {
        this.moduleID = moduleID;
    }

    public Action<MemberRef?, string> GetChecker(Origin origin) {

        var role = new RoleInfo() { AllowAllConfigChanges = true };

        if (origin.Type == OriginType.User) {
            if (!allowedConfigChangesPerRole.TryGetValue(origin.UserRole, out role)) {
                throw new Exception($"Failed to get permissions for user role");
            }
        }

        return (MemberRef? m, string err) => {

            if (m is null) { // the entire root object is to be changed
                if (!role.AllowAllConfigChanges) {
                    throw new Exception(err);
                }
            }
            else {
                if (!role.AllowAllConfigChanges && !role.ChangeableMembers.Contains(m.Value)) {
                    throw new Exception(err);
                }
            }
        };
    }

    public void InitUserRoles(
        IReadOnlyList<ObjectInfo> allObjectInfos,
        Func<ObjectRef, ObjectRef?> getParent,
        IReadOnlyList<Role> roles, 
        MetaInfos meta) {

        Dictionary<string, string[]> mapMembers = new();

        foreach (ClassInfo cls in meta.Classes) {
            mapMembers[cls.Name] = cls.SimpleMember.Select(m => m.Name)
                .Concat(cls.ObjectMember.Select(m => m.Name))
                .ToArray();
        }

        allowedConfigChangesPerRole.Clear();
        foreach (var role in roles) {
            allowedConfigChangesPerRole[role.Name] = InitUserRole(role, allObjectInfos, getParent, mapMembers);
        }
    }

    private RoleInfo InitUserRole(Role role, IReadOnlyList<ObjectInfo> allObjectInfos, Func<ObjectRef, ObjectRef?> getParent, Dictionary<string, string[]> mapMembers) {

        string[] GetMembersOfClass(string className) {
            mapMembers.TryGetValue(className, out string[]? members);
            return members ?? Array.Empty<string>();
        }

        if (!role.RestrictConfigChanges) {
            return new RoleInfo() { AllowAllConfigChanges = true };
        }

        var info = new RoleInfo() { AllowAllConfigChanges = false };

        foreach (ConfigRule x in role.ConfigRules) {
            ObjectRef root = x.RootObject;
            if (root.ModuleID != moduleID) continue;

            string[]? types = x.ObjectTypes == "*" ? null : x.ObjectTypes.Split(',');
            string[]? members = x.Members == "*" ? null : x.Members.Split(',');

            bool add = x.Mode == Mode.Allow;

            string id_pattern = Regex
                .Escape(x.WithID)
                .Replace("\\*", ".*")
                .Replace("\\+", ".+")
                .Replace("\\?", ".");

            Regex regex = new($"^{id_pattern}$");

            foreach (ObjectInfo theObject in allObjectInfos) {
                string className = theObject.ClassNameShort;
                if (types == null || types.Any(t => t == className)) {
                    if (theObject.ID == root || IsChildOf(theObject.ID, root, getParent)) {
                        if (regex.IsMatch(theObject.ID.LocalObjectID)) {
                            string[] theMembers = members ?? GetMembersOfClass(className);
                            foreach (string member in theMembers) {
                                MemberRef m = MemberRef.Make(theObject.ID, member);
                                if (add) {
                                    info.ChangeableMembers.Add(m);
                                }
                                else {
                                    info.ChangeableMembers.Remove(m);
                                }
                            }
                        }
                    }
                }
            }
        }

        return info;
    }

    private static bool IsChildOf(ObjectRef theObject, ObjectRef root, Func<ObjectRef, ObjectRef?> getParent) {
        ObjectRef? parentObjectInfo = getParent(theObject);
        if (!parentObjectInfo.HasValue) return false;
        if (parentObjectInfo == root) return true;
        return IsChildOf(parentObjectInfo.Value, root, getParent);
    }

}
