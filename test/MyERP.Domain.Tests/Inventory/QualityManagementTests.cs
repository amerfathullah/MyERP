using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class QualityManagementTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void QualityGoal_Creation_And_AddObjectives()
    {
        var goal = new QualityGoal(Guid.NewGuid(), "Zero Defect Target", "Monthly", 99.5m, _tenantId)
        {
            Goal = "Maintain 99.5% first-pass yield across all production lines",
            Uom = "%",
            Weekday = "Monday",
            DayOfMonth = 1
        };

        var obj1 = new QualityGoalObjective(Guid.NewGuid(), goal.Id, "SMT Line Yield", 99.8m, "%");
        var obj2 = new QualityGoalObjective(Guid.NewGuid(), goal.Id, "Assembly Line Yield", 99.2m, "%");

        goal.AddObjective(obj1);
        goal.AddObjective(obj2);

        goal.Name.ShouldBe("Zero Defect Target");
        goal.Frequency.ShouldBe("Monthly");
        goal.TargetValue.ShouldBe(99.5m);
        goal.IsEnabled.ShouldBeTrue();
        goal.Objectives.Count.ShouldBe(2);
        goal.Objectives[0].Objective.ShouldBe("SMT Line Yield");
        goal.Objectives[1].Target.ShouldBe(99.2m);
    }

    [Fact]
    public void QualityReview_EvaluateStatus_Rollup_AllPassed_ReturnsPassed()
    {
        var goalId = Guid.NewGuid();
        var review = new QualityReview(Guid.NewGuid(), goalId, DateTime.UtcNow, _tenantId);

        var obj1 = new QualityReviewObjective(Guid.NewGuid(), review.Id, "Tolerance Check", 0.05m, "mm")
        {
            Actual = 0.03m,
            Status = QualityReviewStatus.Passed
        };
        var obj2 = new QualityReviewObjective(Guid.NewGuid(), review.Id, "Finish Quality", 100m, "%")
        {
            Actual = 100m,
            Status = QualityReviewStatus.Passed
        };

        review.AddObjective(obj1);
        review.AddObjective(obj2);

        review.Status.ShouldBe(QualityReviewStatus.Passed);
    }

    [Fact]
    public void QualityReview_EvaluateStatus_Rollup_AnyFailed_ReturnsFailed()
    {
        var goalId = Guid.NewGuid();
        var review = new QualityReview(Guid.NewGuid(), goalId, DateTime.UtcNow, _tenantId);

        var obj1 = new QualityReviewObjective(Guid.NewGuid(), review.Id, "Tolerance Check", 0.05m, "mm")
        {
            Actual = 0.03m,
            Status = QualityReviewStatus.Passed
        };
        var obj2 = new QualityReviewObjective(Guid.NewGuid(), review.Id, "Solder Integrity", 100m, "%")
        {
            Actual = 85m,
            Status = QualityReviewStatus.Failed
        };

        review.AddObjective(obj1);
        review.AddObjective(obj2);

        review.Status.ShouldBe(QualityReviewStatus.Failed);
    }

    [Fact]
    public void QualityReview_EvaluateStatus_Rollup_SomeOpen_ReturnsOpen()
    {
        var goalId = Guid.NewGuid();
        var review = new QualityReview(Guid.NewGuid(), goalId, DateTime.UtcNow, _tenantId);

        var obj1 = new QualityReviewObjective(Guid.NewGuid(), review.Id, "Tolerance Check", 0.05m, "mm")
        {
            Actual = 0.03m,
            Status = QualityReviewStatus.Passed
        };
        var obj2 = new QualityReviewObjective(Guid.NewGuid(), review.Id, "Visual Inspection", 100m, "%")
        {
            Status = QualityReviewStatus.Open
        };

        review.AddObjective(obj1);
        review.AddObjective(obj2);

        review.Status.ShouldBe(QualityReviewStatus.Open);
    }

    [Fact]
    public void QualityAction_Resolutions_Evaluation_And_Lifecycle()
    {
        var action = new QualityAction(Guid.NewGuid(), _companyId, QualityActionType.Corrective, "High solder bridge rate", _tenantId);

        action.Status.ShouldBe(QualityActionStatus.Open);

        var res1 = new QualityActionResolution(Guid.NewGuid(), action.Id, "Nozzle temperature drift", "Recalibrate thermal profile")
        {
            Status = QualityActionStatus.Resolved
        };
        var res2 = new QualityActionResolution(Guid.NewGuid(), action.Id, "Flux viscosity high", "Switch flux batch")
        {
            Status = QualityActionStatus.Resolved
        };

        action.AddResolution(res1);
        action.AddResolution(res2);

        action.Status.ShouldBe(QualityActionStatus.Resolved);

        action.Close();
        action.Status.ShouldBe(QualityActionStatus.Closed);

        // Cannot resolve a closed action
        Should.Throw<BusinessException>(() => action.Resolve("Additional fix"));
    }

    [Fact]
    public void QualityProcedure_Hierarchy_And_Steps()
    {
        var root = new QualityProcedure(Guid.NewGuid(), "Standard Operating Procedures", null, _tenantId)
        {
            IsGroup = true
        };

        var child = new QualityProcedure(Guid.NewGuid(), "Incoming Inspection SOP", root.Id, _tenantId)
        {
            ProcessOwner = "QC Lead",
            Sequence = 1
        };

        child.AddStep(new QualityProcedureStep(Guid.NewGuid(), child.Id, "Verify PO barcode and supplier tag", 1));
        child.AddStep(new QualityProcedureStep(Guid.NewGuid(), child.Id, "Perform AQL sampling test", 2));
        child.AddStep(new QualityProcedureStep(Guid.NewGuid(), child.Id, "Record readings in Quality Inspection", 3));

        child.ParentQualityProcedureId.ShouldBe(root.Id);
        child.Steps.Count.ShouldBe(3);
        child.Steps[1].Description.ShouldBe("Perform AQL sampling test");
        child.Steps[1].Sequence.ShouldBe(2);
    }

    [Fact]
    public void NonConformance_StateTransitions()
    {
        var nc = new NonConformance(Guid.NewGuid(), _companyId, "Cracked housing on batch 4021", Guid.NewGuid(), _tenantId)
        {
            ProcessOwner = "Mold Operator A",
            Details = "Microcracks visible under 10x magnification after cooling cycle."
        };

        nc.Status.ShouldBe(NonConformanceStatus.Open);
        nc.ResolutionDate.ShouldBeNull();

        nc.Resolve("Adjust mold hold pressure by +10%", "Inspect first 50 parts of each shift");
        nc.Status.ShouldBe(NonConformanceStatus.Resolved);
        nc.ResolutionDate.ShouldNotBeNull();
        nc.CorrectiveAction.ShouldBe("Adjust mold hold pressure by +10%");
        nc.PreventiveAction.ShouldBe("Inspect first 50 parts of each shift");

        nc.Reopen();
        nc.Status.ShouldBe(NonConformanceStatus.Open);
        nc.ResolutionDate.ShouldBeNull();

        nc.Cancel();
        nc.Status.ShouldBe(NonConformanceStatus.Cancelled);

        // Cannot resolve cancelled NC
        Should.Throw<BusinessException>(() => nc.Resolve("Test"));
    }

    [Fact]
    public void QualityMeeting_Agendas_And_Minutes()
    {
        var meeting = new QualityMeeting(Guid.NewGuid(), _companyId, DateTime.UtcNow, "Dr. Quality Director", _tenantId)
        {
            Attendees = "Alice, Bob, Charlie, QC Team"
        };

        meeting.Status.ShouldBe(QualityMeetingStatus.Open);

        meeting.AddAgenda(new QualityMeetingAgenda(Guid.NewGuid(), meeting.Id, "Review Q2 Non-Conformance Pareto Chart"));
        meeting.AddAgenda(new QualityMeetingAgenda(Guid.NewGuid(), meeting.Id, "ISO 9001 internal audit preparation"));

        meeting.AddMinute(new QualityMeetingMinutes(Guid.NewGuid(), meeting.Id, "Discussed high defect rate on Line 3.", "Schedule recalibration by Friday", Guid.NewGuid()));

        meeting.Agendas.Count.ShouldBe(2);
        meeting.Minutes.Count.ShouldBe(1);
        meeting.Minutes[0].Discussion.ShouldBe("Discussed high defect rate on Line 3.");

        meeting.Close();
        meeting.Status.ShouldBe(QualityMeetingStatus.Closed);
    }

    [Fact]
    public void QualityFeedback_Template_And_Ratings_Clamped()
    {
        var template = new QualityFeedbackTemplate(Guid.NewGuid(), "Customer Post-Delivery Survey", _tenantId);
        template.AddParameter(new QualityFeedbackTemplateParameter(Guid.NewGuid(), template.Id, "Packaging Quality"));
        template.AddParameter(new QualityFeedbackTemplateParameter(Guid.NewGuid(), template.Id, "Delivery Promptness"));
        template.AddParameter(new QualityFeedbackTemplateParameter(Guid.NewGuid(), template.Id, "Product Quality"));

        template.Parameters.Count.ShouldBe(3);

        var feedback = new QualityFeedback(Guid.NewGuid(), _companyId, QualityFeedbackDocumentType.Customer, "Acme Corp Delivery 1002", template.Id, _tenantId)
        {
            Remarks = "Overall very satisfied."
        };

        // Ratings clamped between 1 and 5
        feedback.AddParameter(new QualityFeedbackParameter(Guid.NewGuid(), feedback.Id, "Packaging Quality", 5, "Excellent"));
        feedback.AddParameter(new QualityFeedbackParameter(Guid.NewGuid(), feedback.Id, "Delivery Promptness", 10, "Super fast")); // > 5 clamped to 5
        feedback.AddParameter(new QualityFeedbackParameter(Guid.NewGuid(), feedback.Id, "Product Quality", 0, "No issues")); // < 1 clamped to 1

        feedback.Parameters.Count.ShouldBe(3);
        feedback.Parameters[0].Rating.ShouldBe(5);
        feedback.Parameters[1].Rating.ShouldBe(5); // Clamped
        feedback.Parameters[2].Rating.ShouldBe(1); // Clamped
    }
}
