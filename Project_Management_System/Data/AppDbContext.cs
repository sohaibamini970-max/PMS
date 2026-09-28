using Microsoft.EntityFrameworkCore;
using Project_Management_System.Models;

namespace Project_Management_System.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();
        public DbSet<TaskSubtask> TaskSubtasks => Set<TaskSubtask>();

        public DbSet<Document> Documents => Set<Document>();
        public DbSet<TaskChallenge> TaskChallenges => Set<TaskChallenge>();

        // ⬇️ NEW
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Organization
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.ToTable("organizations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Slug).HasColumnName("slug");
                entity.Property(e => e.LogoUrl).HasColumnName("logo_url");
                entity.Property(e => e.PrimaryColor).HasColumnName("primary_color");
                entity.Property(e => e.ContactEmail).HasColumnName("contact_email");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.ProfilePictureUrl).HasColumnName("profile_picture_url");
                entity.Property(e => e.Role).HasColumnName("role").HasConversion<string>();
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>();
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
                entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(e => e.Organization)
                      .WithMany(o => o.Users)
                      .HasForeignKey(e => e.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CreatedBy)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ─────────────────────────────────
            // TEAM
            // ─────────────────────────────────
            modelBuilder.Entity<Team>(e =>
            {
                e.ToTable("teams");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.OrganizationId).HasColumnName("organization_id");
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.Description).HasColumnName("description");
                e.Property(x => x.Color).HasColumnName("color");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
                e.Property(x => x.LeaderId).HasColumnName("leader_id");

                e.HasOne(x => x.Organization)
                 .WithMany()
                 .HasForeignKey(x => x.OrganizationId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Leader)
                 .WithMany()
                 .HasForeignKey(x => x.LeaderId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ─────────────────────────────────
            // TEAM MEMBER (join table)
            // ─────────────────────────────────
            modelBuilder.Entity<TeamMember>(e =>
            {
                e.ToTable("team_members");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.TeamId).HasColumnName("team_id");
                e.Property(x => x.UserId).HasColumnName("user_id");
                e.Property(x => x.AddedAt).HasColumnName("added_at");

                e.HasOne(x => x.Team)
                 .WithMany(t => t.Members)
                 .HasForeignKey(x => x.TeamId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                // One user can only appear once per team
                e.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
            });

            // ─────────────────────────────────
            // PROJECT
            // ─────────────────────────────────
            modelBuilder.Entity<Project>(e =>
            {
                e.ToTable("projects");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.OrganizationId).HasColumnName("organization_id");
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.Client).HasColumnName("client");
                e.Property(x => x.Description).HasColumnName("description");
                e.Property(x => x.Domain).HasColumnName("domain");
                e.Property(x => x.Priority).HasColumnName("priority").HasConversion<string>();
                e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();

                e.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date");
                e.Property(x => x.Deadline).HasColumnName("deadline").HasColumnType("date");

                e.Property(x => x.Budget).HasColumnName("budget");
                e.Property(x => x.Progress).HasColumnName("progress");
                e.Property(x => x.ProjectManagerId).HasColumnName("project_manager_id");
                e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                // ⬇️ NEW
                e.Property(x => x.AssignedTeamId).HasColumnName("assigned_team_id");
                e.Property(x => x.AssignedMemberId).HasColumnName("assigned_member_id");

                e.HasOne(x => x.Organization)
                 .WithMany()
                 .HasForeignKey(x => x.OrganizationId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.ProjectManager)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectManagerId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.CreatedBy)
                 .WithMany()
                 .HasForeignKey(x => x.CreatedByUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                // ⬇️ NEW
                e.HasOne(x => x.AssignedTeam)
                 .WithMany(t => t.Projects)
                 .HasForeignKey(x => x.AssignedTeamId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.AssignedMember)
                 .WithMany()
                 .HasForeignKey(x => x.AssignedMemberId)
                 .OnDelete(DeleteBehavior.SetNull);
            });


            // TASK
            modelBuilder.Entity<TaskItem>(e =>
            {
                e.ToTable("tasks");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.OrganizationId).HasColumnName("organization_id");
                e.Property(x => x.ProjectId).HasColumnName("project_id");
                e.Property(x => x.Title).HasColumnName("title");
                e.Property(x => x.Description).HasColumnName("description");
                e.Property(x => x.Objectives).HasColumnName("objectives");
                e.Property(x => x.InstructionsText).HasColumnName("instructions_text");
                e.Property(x => x.InstructionsFileUrl).HasColumnName("instructions_file_url");
                e.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date");
                e.Property(x => x.Deadline).HasColumnName("deadline").HasColumnType("date");
                e.Property(x => x.Priority).HasColumnName("priority").HasConversion<string>();
                e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
                e.Property(x => x.AssigneeId).HasColumnName("assignee_id");
                e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
                e.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
                e.Property(x => x.SubmittedById).HasColumnName("submitted_by_id");
                e.Property(x => x.CompletedAt).HasColumnName("completed_at");
                e.Property(x => x.CompletedById).HasColumnName("completed_by_id");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                // ⬇️ NEW
                e.Property(x => x.AssigneeTeamId).HasColumnName("assignee_team_id");

                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

                // ⬇️ NEW
                e.HasOne(x => x.AssigneeTeam)
                 .WithMany(t => t.Tasks)
                 .HasForeignKey(x => x.AssigneeTeamId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // SUBTASK
            modelBuilder.Entity<TaskSubtask>(e =>
            {
                e.ToTable("task_subtasks");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.TaskId).HasColumnName("task_id");
                e.Property(x => x.Title).HasColumnName("title");
                e.Property(x => x.Description).HasColumnName("description");
                e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
                e.Property(x => x.OrderIndex).HasColumnName("order_index");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
                e.HasOne(x => x.Task).WithMany(t => t.Subtasks).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            });

            // CHALLENGE
            modelBuilder.Entity<TaskChallenge>(e =>
            {
                e.ToTable("task_challenges");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.TaskId).HasColumnName("task_id");
                e.Property(x => x.RaisedByUserId).HasColumnName("raised_by_user_id");
                e.Property(x => x.Title).HasColumnName("title");
                e.Property(x => x.Description).HasColumnName("description");
                e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
                e.HasOne(x => x.Task).WithMany(t => t.Challenges).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.RaisedBy).WithMany().HasForeignKey(x => x.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Document>(e =>
            {
                e.ToTable("documents");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.OrganizationId).HasColumnName("organization_id");
                e.Property(x => x.ProjectId).HasColumnName("project_id");
                e.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id");
                e.Property(x => x.Title).HasColumnName("title");
                e.Property(x => x.OriginalFileName).HasColumnName("original_file_name");
                e.Property(x => x.StoredFileName).HasColumnName("stored_file_name");
                e.Property(x => x.FilePath).HasColumnName("file_path");
                e.Property(x => x.FileSize).HasColumnName("file_size");
                e.Property(x => x.ContentType).HasColumnName("content_type");
                e.Property(x => x.DocumentType).HasColumnName("document_type");
                e.Property(x => x.Notes).HasColumnName("notes");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}