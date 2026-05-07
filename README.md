# Online School Diary

**Tech**: C# (.NET), Windows Forms client + TCP JSON server  
**Storage**: JSON files on server (**no database**)

## Projects

- `OnlineSchoolDiary.Server`: TCP server (`127.0.0.1:5050`) + JSON-file persistence
- `OnlineSchoolDiary.Client`: WinForms client with role dashboards
- `OnlineSchoolDiary.Shared`: Models + protocol DTOs used by both

## Seed Accounts (first run)

- **Admin**: `admin / admin`
- **Teacher**: `ali / 123`
- **Student**: `student1 / 123`
- **Parent**: `parent1 / 123` (linked to `student1`)

## How to Run

1. Start the server:
   - Run `OnlineSchoolDiary.Server`
2. Start the client:
   - Run `OnlineSchoolDiary.Client`
3. Login and use your dashboard based on role.

## Modules Implemented

- Login system with roles: Admin / Teacher / Student / Parent
- Admin:
  - Manage users (add/update/delete/view)
  - Assign classes & subjects to teachers
  - Send notifications (all / by role / by class / by user)
  - View basic reports
- Teacher:
  - Create/edit/delete diary entries for assigned class/subject
  - Chat with parents
  - View completion counts (students who acknowledged diary)
- Student:
  - View diary/assignments
  - Acknowledge (checkbox-style) completion per diary entry
  - View notifications
- Parent:
  - View child diary
  - Chat with teacher

