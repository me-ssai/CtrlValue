using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCtrlValueAgentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptTemplateVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InputPayload = table.Column<string>(type: "text", nullable: true),
                    OutputPayload = table.Column<string>(type: "text", nullable: true),
                    ToolsUsed = table.Column<string>(type: "text", nullable: true),
                    SourcesUsed = table.Column<string>(type: "text", nullable: true),
                    SafetyDecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_context_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_context_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SectionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_conversations_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_conversations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_feature_flags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_feature_flags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_insights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    InsightType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDismissed = table.Column<bool>(type: "boolean", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_insights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_web_research_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Query = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Sources = table.Column<string>(type: "text", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_web_research_cache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    StructuredPayload = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    ToolCalls = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_messages_agent_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "agent_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_feature_flag_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureFlagId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_feature_flag_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_feature_flag_assignments_agent_feature_flags_FeatureF~",
                        column: x => x.FeatureFlagId,
                        principalTable: "agent_feature_flags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_feature_flag_assignments_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_feature_flag_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_audit_logs_UserId_CreatedAt",
                table: "agent_audit_logs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_context_snapshots_EntityId_SnapshotType",
                table: "agent_context_snapshots",
                columns: new[] { "EntityId", "SnapshotType" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_conversations_EntityId_UserId",
                table: "agent_conversations",
                columns: new[] { "EntityId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_conversations_UserId",
                table: "agent_conversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feature_flag_assignments_EntityId",
                table: "agent_feature_flag_assignments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feature_flag_assignments_FeatureFlagId_UserId",
                table: "agent_feature_flag_assignments",
                columns: new[] { "FeatureFlagId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_feature_flag_assignments_UserId",
                table: "agent_feature_flag_assignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feature_flags_Key",
                table: "agent_feature_flags",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_insights_EntityId_InsightType_IsDismissed",
                table: "agent_insights",
                columns: new[] { "EntityId", "InsightType", "IsDismissed" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_messages_ConversationId",
                table: "agent_messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_web_research_cache_TopicKey",
                table: "agent_web_research_cache",
                column: "TopicKey",
                unique: true);

            // -- Seed: 9 agent feature flag sections (all disabled by default) --
            // SuperAdmin enables them per-user via the Agent Settings admin tab.
            var now = DateTime.UtcNow;
            var seedSections = new[]
            {
                (Guid.Parse("a1000000-0000-0000-0000-000000000001"), "agent_core",          "Agent Core",               "AgentCore"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000002"), "personal_finance",    "Personal Finance Context", "PersonalFinance"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000003"), "macro_insights",      "Macro Insights",           "MacroInsights"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000004"), "net_worth_analysis",  "Net Worth Growth Analysis","NetWorthAnalysis"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000005"), "liability_review",    "Liability & Asset Review", "LiabilityReview"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000006"), "chat",                "Conversational Chat",      "ConversationalChat"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000007"), "scenario_exploration","Scenario Exploration",     "ScenarioExploration"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000008"), "alerts_nudges",       "Alerts & Nudges",          "AlertsNudges"),
                (Guid.Parse("a1000000-0000-0000-0000-000000000009"), "explanation_mode",    "Explanation / Audit Mode", "ExplanationMode"),
            };

            foreach (var (id, key, name, sectionKey) in seedSections)
            {
                migrationBuilder.InsertData(
                    table: "agent_feature_flags",
                    columns: new[] { "Id", "Key", "Name", "SectionKey", "IsEnabled", "TenantId", "CreatedAt", "IsDeleted" },
                    values: new object[] { id, key, name, sectionKey, false, "", now, false });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_audit_logs");

            migrationBuilder.DropTable(
                name: "agent_context_snapshots");

            migrationBuilder.DropTable(
                name: "agent_feature_flag_assignments");

            migrationBuilder.DropTable(
                name: "agent_insights");

            migrationBuilder.DropTable(
                name: "agent_messages");

            migrationBuilder.DropTable(
                name: "agent_web_research_cache");

            migrationBuilder.DropTable(
                name: "agent_feature_flags");

            migrationBuilder.DropTable(
                name: "agent_conversations");

        }
    }
}
