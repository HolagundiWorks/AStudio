// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace AStudio.App.Services;

/// <summary>Web AStudio studioNav labels — taskbar peers + menus (NAVIGATION.md).</summary>
public enum StageId
{
    Home,
    Projects,
    ProjectFocus,
    Clients,
    Tasks,
    Stub,
}

public static class StudioNav
{
    public const string PeerProjects = "Projects";
    public const string PeerClients = "Clients";
    public const string PeerPeople = "People";
    public const string PeerOffice = "Office";
    public const string PeerFinance = "Finance";
    public const string PeerAdmin = "Admin";

    public static readonly (string Id, string Label, string Blurb)[] PeopleItems =
    [
        ("teams", "Teams", "Roster and assignments — desktop slice later."),
        ("work", "Work / Tasks", "Local task board (live)."),
        ("performance", "Performance", "ASPRF dashboard — desktop slice later."),
        ("hr", "HR", "Leaves and payroll gates — desktop slice later."),
    ];

    public static readonly (string Id, string Label, string Blurb)[] OfficeItems =
    [
        ("leads", "Leads", "Enquiry capture — desktop slice later."),
        ("tenders", "Tenders", "Firm-issued tenders — desktop slice later."),
        ("proposals", "Proposals", "COA fee + scope — use Focus › Fees for now."),
        ("documents", "Documents", "Office documents — desktop slice later."),
        ("contracts", "Contracts", "Contracts register — desktop slice later."),
        ("letters", "Letters", "Letters — desktop slice later."),
    ];

    public static readonly (string Id, string Label, string Blurb)[] FinanceItems =
    [
        ("invoices", "Invoices", "Fee / invoice stubs live under project Focus › Fees."),
        ("reconcile", "Reconcile", "Bank / GST reconcile — desktop slice later."),
        ("cashbook", "Cashbook", "Office cash book — desktop slice later."),
        ("expenses", "Office Expenses", "Expenses — desktop slice later."),
        ("payroll", "Payroll", "Payslips — desktop slice later."),
        ("reports", "Financial Reports", "GST/TDS abstracts — desktop slice later."),
    ];

    public static readonly (string Id, string Label, string Blurb)[] AdminItems =
    [
        ("consultants", "Consultants", "Third parties — desktop slice later."),
        ("contractors", "Contractors", "Third parties — desktop slice later."),
        ("vendors", "Vendors", "Third parties — desktop slice later."),
        ("spec", "Library · Specification", "Spec catalogue — desktop slice later."),
        ("ratebooks", "Library · Rate Books", "Rate books — desktop slice later."),
        ("compliance", "Library · Compliance", "Codes library — desktop slice later."),
        ("standards", "Library · Standards", "Standards — desktop slice later."),
        ("connection", "Connection manager", "Activate in AORMS Connect; Flush live in taskbar tray."),
        ("archived", "Archived projects", "Archive browser — desktop slice later."),
    ];
}
