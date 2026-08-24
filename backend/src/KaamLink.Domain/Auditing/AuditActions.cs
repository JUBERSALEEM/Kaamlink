namespace KaamLink.Domain.Auditing;

/// <summary>
/// Central registry of audited action names — audit action strings never appear
/// as magic strings elsewhere in the codebase.
/// </summary>
public static class AuditActions
{
    public const string UserRegistered = "auth.user_registered";
    public const string OtpRequested = "auth.otp_requested";
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string TokenRefreshed = "auth.token_refreshed";
    public const string TokenReuseDetected = "auth.token_reuse_detected";
    public const string TokenRevoked = "auth.token_revoked";
    public const string ForeignTokenRevokeAttempt = "auth.token_foreign_revoke_attempt";

    public const string WorkerProfileUpdated = "worker.profile_updated";
    public const string WorkerSkillsUpdated = "worker.skills_updated";
    public const string CompanyRegistered = "company.registered";
    public const string CompanyVerified = "company.verified";
    public const string CompanyRejected = "company.rejected";

    public const string VacancyCreated = "jobs.vacancy_created";
    public const string VacancyUpdated = "jobs.vacancy_updated";
    public const string VacancyStatusChanged = "jobs.vacancy_status_changed";
    public const string ApplicationSubmitted = "jobs.application_submitted";
    public const string ApplicationStatusChanged = "jobs.application_status_changed";
    public const string ApplicationWithdrawn = "jobs.application_withdrawn";

    // Phase 05 — skill assessments (no sensitive payloads in metadata: score + skill id only).
    public const string AssessmentAttemptStarted = "assessment.attempt_started";
    public const string AssessmentSubmitted = "assessment.submitted";
    public const string WorkerSkillAssessmentVerified = "worker.skill_assessment_verified";
}
