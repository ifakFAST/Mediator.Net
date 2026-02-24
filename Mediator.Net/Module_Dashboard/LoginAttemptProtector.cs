// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ifak.Fast.Mediator.Dashboard;

internal sealed class LoginAttemptProtector
{
    private const int MaxFailedAttempts = 10;
    private static readonly Duration FailedAttemptWindow = Duration.FromMinutes(15);

    private readonly HashSet<string> validUsers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Timestamp>> failedAttemptsByUser = new(StringComparer.Ordinal);

    public void UpdateValidUsers(IEnumerable<string> users) {

        validUsers.Clear();
        foreach (string user in users.Where(u => !string.IsNullOrWhiteSpace(u))) {
            validUsers.Add(user);
        }

        string[] removedUsers = failedAttemptsByUser.Keys.Where(user => !validUsers.Contains(user)).ToArray();
        foreach (string removedUser in removedUsers) {
            failedAttemptsByUser.Remove(removedUser);
        }
    }

    public bool TryAllowLogin(string login, out string rejectReason) {

        if (!validUsers.Contains(login)) {
            rejectReason = "Invalid login or password."; // Do not reveal whether the login or the password is incorrect.
            return false;
        }

        if (IsBlocked(login)) {
            rejectReason = "Login temporarily blocked due to too many failed attempts.";
            return false;
        }

        rejectReason = "";
        return true;
    }

    /// <summary>
    /// Registers a failed login attempt for the specified user and determines whether the maximum allowed number of
    /// failed attempts has been exceeded.
    /// </summary>
    /// <remarks>If the specified user is not valid, the method returns false without registering the attempt.
    /// Expired attempts are removed before the new attempt is added.</remarks>
    /// <param name="login">The username of the user attempting to log in. Must correspond to a valid user; otherwise, the attempt is not
    /// registered.</param>
    /// <returns>true if the number of failed attempts for the user exceeds the maximum allowed; otherwise, false.</returns>
    public bool RegisterFailedAttempt(string login) {

        if (!validUsers.Contains(login)) {
            return false;
        }

        if (!failedAttemptsByUser.TryGetValue(login, out List<Timestamp>? attempts)) {
            attempts = [];
            failedAttemptsByUser[login] = attempts;
        }

        PruneExpiredAttempts(attempts);
        attempts.Add(Timestamp.Now);
        return attempts.Count > MaxFailedAttempts;
    }

    private bool IsBlocked(string login) {

        if (!failedAttemptsByUser.TryGetValue(login, out List<Timestamp>? attempts)) {
            return false;
        }

        PruneExpiredAttempts(attempts);
        return attempts.Count > MaxFailedAttempts;
    }

    private static void PruneExpiredAttempts(List<Timestamp> attempts) {

        Timestamp minAllowed = Timestamp.Now - FailedAttemptWindow;

        int removeCount = 0;
        while (removeCount < attempts.Count && attempts[removeCount] < minAllowed) {
            removeCount += 1;
        }

        if (removeCount > 0) {
            attempts.RemoveRange(0, removeCount);
        }
    }
}
