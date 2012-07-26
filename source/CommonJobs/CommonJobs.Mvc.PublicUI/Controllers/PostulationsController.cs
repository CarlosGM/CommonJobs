﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CommonJobs.Domain;
using CommonJobs.Infrastructure.AttachmentStorage;
using CommonJobs.Infrastructure.SharedLinks;
using CommonJobs.Raven.Mvc;

namespace CommonJobs.Mvc.PublicUI.Controllers
{
    public class PostulationsController : CommonJobsController
    {
        public ActionResult Create(string sharedCode)
        {
            var id = Query(new SearchSharedEntity(sharedCode));
            if (id == null)
                return HttpNotFound();

            var jobSearch = RavenSession.Load<JobSearch>(id);
            if (jobSearch == null)
                return HttpNotFound();

            var md = new MarkdownDeep.Markdown();

            ViewBag.Title = jobSearch.Title;
            ViewBag.PublicNotes = new MvcHtmlString(md.Transform(jobSearch.PublicNotes));

            return View();
        } 

        private TemporalFileReference SaveTemporalFile(HttpPostedFileBase file)
        {
            var temporalFolderPath = System.Configuration.ConfigurationManager.AppSettings["CommonJobs/TemporalUploadsPath"];
            var internalFileName = Guid.NewGuid().ToString();
            var temporalFullName = Path.Combine(temporalFolderPath, internalFileName);
            file.SaveAs(temporalFullName);
            return new TemporalFileReference() 
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                InternalFileName = internalFileName
            };
        }

        private Applicant GenerateApplicant(Postulation postulation)
        {
            //TODO: move it to an Action
            var applicant = new Applicant()
            {
                Email = postulation.Email,
                FirstName = postulation.FirstName,
                LastName = postulation.LastName
            };
            RavenSession.Store(applicant);

            var curriculum = GenerateAttachment(applicant, postulation.Curriculum);
            applicant.Notes.Add(new ApplicantNote()
            {
                Note = "Curriculum",
                NoteType = ApplicantNoteType.GeneralNote,
                RealDate = DateTime.Now,
                RegisterDate = DateTime.Now,
                Attachment = curriculum
            });

            return applicant;
        }

        private AttachmentReference GenerateAttachment(object entity, TemporalFileReference temporalReference)
        {
            AttachmentReference result;
            var temporalFolderPath = System.Configuration.ConfigurationManager.AppSettings["CommonJobs/TemporalUploadsPath"];
            var temporalFilePath = Path.Combine(temporalFolderPath, temporalReference.InternalFileName);
            using (var stream = System.IO.File.OpenRead(temporalFilePath))
            {
                result = ExecuteCommand(new SaveAttachment(
                    entity,
                    temporalReference.OriginalFileName,
                    stream));
            }
            System.IO.File.Delete(temporalFilePath);
            return result;
        }


        [HttpPost]
        public ActionResult Create(Postulation postulation, HttpPostedFileBase curriculumFile)
        {
            try
            {
                //TODO: Validate
                postulation.Curriculum = SaveTemporalFile(curriculumFile);

                //TODO: do it asynchronously?
                GenerateApplicant(postulation);

                return RedirectToAction("index", "Home");
            }
            catch
            {
                return View();
            }
        }
    }
}
