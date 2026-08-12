// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace AStudio.App.Models;

public sealed class ProjectRow
{
    public string ProjectId { get; init; } = "";
    public string Ref { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string Phase { get; init; } = "";
    public string PublishState { get; init; } = "";
}

public sealed class LedgerRow
{
    public string ItemId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Status { get; init; } = "";
    public string PublishState { get; init; } = "";
    public string Notes { get; init; } = "";
}

public sealed class FeeRow
{
    public string FeeId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Amount { get; init; } = "";
    public string Status { get; init; } = "";
    public string PublishState { get; init; } = "";
}

public sealed class DrawingRow
{
    public string DrawingId { get; init; } = "";
    public string Number { get; init; } = "";
    public string Title { get; init; } = "";
    public string Rev { get; init; } = "";
    public string Status { get; init; } = "";
    public string PublishState { get; init; } = "";
    public string HashShort { get; init; } = "";
}

public sealed class ClientRow
{
    public string ClientId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Contact { get; init; } = "";
    public string Email { get; init; } = "";
    public string PublishState { get; init; } = "";
}

public sealed class TaskRow
{
    public string TaskId { get; init; } = "";
    public string ProjectId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string PublishState { get; init; } = "";
}

public sealed class AttentionRow
{
    public string Text { get; init; } = "";
}
