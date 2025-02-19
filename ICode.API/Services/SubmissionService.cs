﻿using AutoMapper;
using CodeStudy.Models;
using Data.Entity;
using Data.Repository;
using Data.Repository.Interfaces;
using ICode.Common;
using Microsoft.EntityFrameworkCore;
using Models.DTO;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace API.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ICodeExecutor _codeExecutor;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITestcaseService _testcaseService;
        private readonly IMapper _mapper;
        public SubmissionService(ISubmissionRepository submissionRepository, IUnitOfWork unitOfWork, ITestcaseService testcaseService, ICodeExecutor codeExecutor, IMapper mapper)
        {
            _submissionRepository = submissionRepository;
            _unitOfWork = unitOfWork;
            _testcaseService = testcaseService;
            _codeExecutor = codeExecutor;
            _mapper = mapper;
        }
        #region CRUD
        public async Task Add(Submission entity)
        {
            await _submissionRepository.AddAsync(entity);
            await _unitOfWork.CommitAsync();
        }

        public Submission FindByID(string ID)
        {
            return _submissionRepository.FindByID(ID);
        }

        public IEnumerable<Submission> GetAll()
        {
            return _submissionRepository.FindAll();
        }

        public async Task<bool> Remove(string ID)
        {
            Submission submission = _submissionRepository.FindByID(ID);
            if (submission == null)
            {
                return false;
            }
            _submissionRepository.Remove(submission);
            await _unitOfWork.CommitAsync();
            return true;
        }

        public Task<bool> Update(string ID, object entity)
        {
            throw new NotImplementedException();
        }
        #endregion
        public SubmissionDTO GetDetail(string Id)
        {
            Submission submission = _submissionRepository.GetSubmissionDetailSingle(x => x.ID == Id);
            return _mapper.Map<Submission, SubmissionDTO>(submission);
        }

        public IEnumerable<SubmissionDetailDTO> GetSubmitDetail(string Id)
        {
            return _mapper.Map<IEnumerable<SubmissionDetail>, IEnumerable<SubmissionDetailDTO>>(_submissionRepository.GetSubmissionDetailSingle(x => x.ID == Id).SubmissionDetails);
        }

        public IEnumerable<SubmissionDTO> GetSubmissionsOfProblem(string problemId)
        {
            return _mapper.Map<IEnumerable<Submission>, IEnumerable<SubmissionDTO>>(_submissionRepository.GetSubmissionsDetail(x => x.SubmissionDetails.First().TestCase.ProblemID == problemId));
        }

        public async Task<PagingList<SubmissionDTO>> GetPageSubmissionsOfProblem(string problemId, int page, int pageSize, string user, string language, bool? status, DateTime? date, string sort, string orderBy)
        {
            PagingList<SubmissionDTO> list = _mapper.Map<PagingList<Submission>, PagingList<SubmissionDTO>>(await _submissionRepository.GetPageAsync(page, pageSize, x => (x.User.Username.Contains(user) || x.User.ID == user) && x.SubmissionDetails.First().TestCase.Problem.ID == problemId && (string.IsNullOrEmpty(language) || x.Language == language) && (status == null || (bool)status == (x.State == SubmitState.Success)) && (date == null || ((DateTime)date).Date == x.CreatedAt.Date), submission => submission.Include(x => x.User).Include(x => x.SubmissionDetails).ThenInclude(x => x.TestCase).ThenInclude(x => x.Problem)));
            if (!string.IsNullOrWhiteSpace(sort))
            {
                switch (sort.ToLower())
                {
                    case "user":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.User.Username) : list.Data.OrderByDescending(x => x.User.Username);
                        break;
                    case "language":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.Language) : list.Data.OrderByDescending(x => x.Language);
                        break;
                    case "status":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.Status) : list.Data.OrderByDescending(x => x.Status);
                        break;
                    case "date":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.CreatedAt) : list.Data.OrderByDescending(x => x.CreatedAt);
                        break;
                    default:
                        throw new Exception("Invalid Action.");
                }
            }
            return list;
        }

        public async Task<SubmissionResult> Submit(Submission submission, string problemID)
        {
            IEnumerable<TestcaseDTO> testcases = _testcaseService.GetTestcaseOfProblem(problemID);
            if (testcases.Count() <= 0)
            {
                return null;
            }
            foreach (TestcaseDTO testcase in testcases)
            {
                ExecutorResult result = await _codeExecutor.ExecuteCode(submission.Code, submission.Language, testcase.Input);
                SubmissionDetail submitDetail = new SubmissionDetail
                {
                    Memory = Convert.ToSingle(result.memory),
                    Time = Convert.ToSingle(result.cpuTime),
                    State = SubmitState.Pending,
                    TestCaseID = testcase.ID,
                };
                if (result.memory == null && result.cpuTime == null)
                {
                    submitDetail.State = SubmitState.CompilerError;
                    submission.SubmissionDetails.Add(submitDetail);
                    break;
                }
                else
                {
                    if (result.output.Replace("\n", "") == testcase.Output.Replace("\r\n", ""))
                    {
                        if (Convert.ToSingle(result.cpuTime) <= testcase.TimeLimit)
                        {
                            if (Convert.ToSingle(result.memory) <= testcase.MemoryLimit)
                            {
                                submitDetail.State = SubmitState.Success;
                            }
                            else
                            {
                                submitDetail.State = SubmitState.MemoryLimit;
                            }
                        }
                        else
                        {
                            submitDetail.State = SubmitState.TimeLimit;
                        }
                    }
                    else
                    {
                        submitDetail.State = SubmitState.WrongAnswer;
                    }
                }
                submission.SubmissionDetails.Add(submitDetail);
            }
            submission.State = submission.SubmissionDetails.FirstOrDefault(x => x.State != SubmitState.Success)?.State ?? SubmitState.Success;
            await _submissionRepository.AddAsync(submission);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<Submission, SubmissionResult>(submission);
        }

        public async Task<PagingList<SubmissionDTO>> GetPageByFilter(int page, int pageSize, string user, string problem, string language, bool? status, DateTime? date, string sort, string orderBy)
        {
            PagingList<SubmissionDTO> list = _mapper.Map<PagingList<Submission>, PagingList<SubmissionDTO>>(await _submissionRepository.GetPageAsync(page, pageSize, x => (x.User.Username.Contains(user) || x.UserID == user) && x.SubmissionDetails.First().TestCase.Problem.Name.Contains(problem) && (string.IsNullOrEmpty(language) || x.Language == language) && (status == null || (bool)status == (x.State == SubmitState.Success)) && (date == null || ((DateTime)date).Date == x.CreatedAt.Date), submission => submission.Include(x => x.User).Include(x => x.SubmissionDetails).ThenInclude(x => x.TestCase).ThenInclude(x => x.Problem)));
            if (!string.IsNullOrWhiteSpace(sort))
            {
                switch (sort.ToLower())
                {
                    case "user":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.User.Username) : list.Data.OrderByDescending(x => x.User.Username);
                        break;
                    case "problem":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.Problem.Name) : list.Data.OrderByDescending(x => x.Problem.Name);
                        break;
                    case "language":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.Language) : list.Data.OrderByDescending(x => x.Language);
                        break;
                    case "status":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.Status) : list.Data.OrderByDescending(x => x.Status);
                        break;
                    case "date":
                        list.Data = (orderBy == "asc") ? list.Data.OrderBy(x => x.CreatedAt) : list.Data.OrderByDescending(x => x.CreatedAt);
                        break;
                    default:
                        throw new Exception("Invalid Action.");
                }
            }
            return list;
        }

        public IEnumerable<SubmissionDTO> GetSubmissionByFilter(string user, string problem, string language, bool? status, DateTime? date, string sort, string orderBy)
        {
            IEnumerable<SubmissionDTO> submissions = _mapper.Map<IEnumerable<Submission>, IEnumerable<SubmissionDTO>>(_submissionRepository.GetSubmissionsDetail(x => x.User.Username.Contains(user) && x.SubmissionDetails.First().TestCase.Problem.Name.Contains(problem) && x.Language.Contains(language) && (status == null || (bool)status == (x.State == SubmitState.Success)) && (date == null || ((DateTime)date).Date == x.CreatedAt.Date)));
            if (!string.IsNullOrWhiteSpace(sort))
            {
                switch (sort.ToLower())
                {
                    case "user":
                        return (orderBy == "asc") ? submissions.OrderBy(x => x.User.Username) : submissions.OrderByDescending(x => x.User.Username);
                    case "problem":
                        return (orderBy == "asc") ? submissions.OrderBy(x => x.Problem.Name) : submissions.OrderByDescending(x => x.Problem.Name);
                    case "language":
                        return (orderBy == "asc") ? submissions.OrderBy(x => x.Language) : submissions.OrderByDescending(x => x.Language);
                    case "status":
                        return (orderBy == "asc") ? submissions.OrderBy(x => x.Status) : submissions.OrderByDescending(x => x.Status);
                    case "date":
                        return (orderBy == "asc") ? submissions.OrderBy(x => x.CreatedAt) : submissions.OrderByDescending(x => x.CreatedAt);
                    default:
                        throw new Exception("Invalid Action.");
                }
            }
            return submissions;
        }
    }
}
