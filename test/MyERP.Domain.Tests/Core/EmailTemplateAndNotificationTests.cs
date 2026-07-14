using System;
using System.Collections.Generic;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class EmailTemplateAndNotificationTests
{
    [Fact]
    public void EmailTemplate_DefaultState()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Payment Reminder",
            "Payment Due: {{ invoice_number }}", "<p>Dear {{ customer_name }},</p>");
        Assert.Equal("Payment Reminder", t.Name);
        Assert.True(t.IsEnabled);
        Assert.Null(t.DocumentType);
    }

    [Fact]
    public void EmailTemplate_RenderSubject()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Reminder",
            "Payment Due: {{invoice_number}}", "Body");
        var vars = new Dictionary<string, string>
        {
            ["invoice_number"] = "INV-2026-001"
        };

        Assert.Equal("Payment Due: INV-2026-001", t.RenderSubject(vars));
    }

    [Fact]
    public void EmailTemplate_RenderBody_MultipleVariables()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Dunning",
            "Overdue Notice", "<p>Dear {{customer_name}}, your invoice {{invoice_number}} is overdue by {{days}} days.</p>");
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = "ABC Corp",
            ["invoice_number"] = "SI-001",
            ["days"] = "30"
        };

        var rendered = t.RenderBody(vars);
        Assert.Contains("ABC Corp", rendered);
        Assert.Contains("SI-001", rendered);
        Assert.Contains("30 days", rendered);
    }

    [Fact]
    public void EmailTemplate_RenderBody_MissingVariable_LeavesPlaceholder()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Test",
            "Subject", "Hello {{name}}, balance is {{amount}}.");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Ahmad"
            // "amount" is missing
        };

        var rendered = t.RenderBody(vars);
        Assert.Contains("Ahmad", rendered);
        Assert.Contains("{{amount}}", rendered); // Unreplaced
    }

    [Fact]
    public void EmailTemplate_RenderBody_EmptyVars_NoChange()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Test",
            "Subject", "No variables here.");
        var rendered = t.RenderBody(new Dictionary<string, string>());
        Assert.Equal("No variables here.", rendered);
    }

    [Fact]
    public void EmailTemplate_NameRequired()
    {
        Assert.Throws<ArgumentException>(() =>
            new EmailTemplate(Guid.NewGuid(), "", "Subject", "Body"));
    }

    [Fact]
    public void EmailTemplate_SubjectRequired()
    {
        Assert.Throws<ArgumentException>(() =>
            new EmailTemplate(Guid.NewGuid(), "Name", "", "Body"));
    }

    [Fact]
    public void EmailTemplate_BodyRequired()
    {
        Assert.Throws<ArgumentException>(() =>
            new EmailTemplate(Guid.NewGuid(), "Name", "Subject", ""));
    }

    [Fact]
    public void NotificationLog_DefaultState()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Payment Reminder", "user@company.com");
        Assert.Equal(NotificationChannel.Email, log.Channel);
        Assert.Equal(NotificationStatus.Queued, log.Status);
        Assert.Equal("Payment Reminder", log.Subject);
        Assert.Equal("user@company.com", log.Recipient);
        Assert.Null(log.SentAt);
        Assert.Null(log.ErrorMessage);
        Assert.Equal(0, log.RetryCount);
    }

    [Fact]
    public void NotificationLog_MarkSent()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Test", "user@test.com");
        log.MarkSent();

        Assert.Equal(NotificationStatus.Sent, log.Status);
        Assert.NotNull(log.SentAt);
    }

    [Fact]
    public void NotificationLog_MarkFailed()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Test", "user@test.com");
        log.MarkFailed("SMTP connection refused");

        Assert.Equal(NotificationStatus.Failed, log.Status);
        Assert.Equal("SMTP connection refused", log.ErrorMessage);
        Assert.Equal(1, log.RetryCount);
    }

    [Fact]
    public void NotificationLog_Retry_MaxAttempts()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Test", "user@test.com");

        log.MarkFailed("Error 1");
        log.QueueRetry(); // retry 1
        Assert.Equal(NotificationStatus.Queued, log.Status);

        log.MarkFailed("Error 2");
        log.QueueRetry(); // retry 2
        Assert.Equal(NotificationStatus.Queued, log.Status);

        log.MarkFailed("Error 3");
        log.QueueRetry(); // retry 3 — max reached
        Assert.Equal(NotificationStatus.PermanentlyFailed, log.Status);
    }

    [Fact]
    public void NotificationLog_InAppChannel()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.InApp,
            "Approval Required", "userId123");
        Assert.Equal(NotificationChannel.InApp, log.Channel);
    }

    [Fact]
    public void NotificationLog_DocumentLink()
    {
        var docId = Guid.NewGuid();
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Invoice Paid", "customer@mail.com")
        {
            DocumentType = "SalesInvoice",
            DocumentId = docId
        };

        Assert.Equal("SalesInvoice", log.DocumentType);
        Assert.Equal(docId, log.DocumentId);
    }
}
