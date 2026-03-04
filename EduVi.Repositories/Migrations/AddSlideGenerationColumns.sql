-- Migration: Add columns for slide generation pipeline
-- Run this against the EduVi database

-- Step 1: Add columns for storing lesson plan text and textbook sections
-- (populated when Service 2 - lesson_analysis completes)
ALTER TABLE Products ADD LessonPlanText nvarchar(MAX) NULL;
ALTER TABLE Products ADD TextbookSections nvarchar(MAX) NULL;

-- Step 2: Add columns for slide generation output
-- (populated when Service 3 - slide_generator completes)
ALTER TABLE Products ADD SlideDocument nvarchar(MAX) NULL;
ALTER TABLE Products ADD SlideGeneratedAt datetime NULL;

GO
