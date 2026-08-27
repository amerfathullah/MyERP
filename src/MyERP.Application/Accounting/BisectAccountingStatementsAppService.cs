using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.BisectAccountingStatements.Default)]
public class BisectAccountingStatementsAppService : MyERPAppService, IBisectAccountingStatementsAppService
{
    private readonly IRepository<BisectAccountingStatements, Guid> _repository;
    private readonly IRepository<BisectNode, Guid> _nodeRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;

    public BisectAccountingStatementsAppService(
        IRepository<BisectAccountingStatements, Guid> repository,
        IRepository<BisectNode, Guid> nodeRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalRepository)
    {
        _repository = repository;
        _nodeRepository = nodeRepository;
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
        _journalRepository = journalRepository;
    }

    public async Task<PagedResultDto<BisectAccountingStatementsDto>> GetListAsync(BisectAccountingStatementsGetListInput input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.FromDate >= input.FromDate.Value.Date);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.ToDate <= input.ToDate.Value.Date);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? $"{nameof(BisectAccountingStatements.CreationTime)} desc" : input.Sorting;

        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount));

        var companies = (await _companyRepository.GetListAsync()).ToDictionary(c => c.Id, c => c.Name);
        var mapper = new BisectAccountingStatementsMapper();

        var dtos = items.Select(x =>
        {
            var dto = mapper.Map(x);
            if (companies.TryGetValue(x.CompanyId, out var name)) dto.CompanyName = name;
            return dto;
        }).ToList();

        return new PagedResultDto<BisectAccountingStatementsDto>(totalCount, dtos);
    }

    public async Task<BisectAccountingStatementsDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var nodesQueryable = await _nodeRepository.GetQueryableAsync();
        var nodes = await AsyncExecuter.ToListAsync(nodesQueryable.Where(n => n.BisectAccountingStatementsId == id));

        var mapper = new BisectAccountingStatementsMapper();
        var dto = mapper.Map(entity);

        var company = await _companyRepository.FindAsync(entity.CompanyId);
        if (company != null) dto.CompanyName = company.Name;

        var nodeMapper = new BisectNodeMapper();
        dto.Nodes = nodes.Select(nodeMapper.Map).ToList();

        return dto;
    }

    [Authorize(MyERPPermissions.BisectAccountingStatements.Create)]
    public async Task<BisectAccountingStatementsDto> CreateAndBuildTreeAsync(CreateBisectAccountingStatementsDto input)
    {
        if (input.FromDate.Date > input.ToDate.Date)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:FromDateGreaterThanToDate", "From Date cannot be greater than To Date.");
        }

        var entity = new BisectAccountingStatements(
            GuidGenerator.Create(),
            input.CompanyId,
            input.FromDate.Date,
            input.ToDate.Date,
            input.Algorithm,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity, autoSave: true);

        // Build tree
        var rootNode = new BisectNode(
            GuidGenerator.Create(),
            entity.Id,
            entity.FromDate,
            entity.ToDate,
            parentNodeId: null,
            CurrentTenant.Id);

        var (rootPl, rootBs) = await CalculateSummaryAsync(entity.CompanyId, entity.FromDate, entity.ToDate);
        rootNode.SetSummary(rootPl, rootBs);
        await _nodeRepository.InsertAsync(rootNode, autoSave: true);

        var createdNodes = new List<BisectNode> { rootNode };

        if (input.Algorithm == BisectAlgorithm.BFS)
        {
            var queue = new Queue<BisectNode>();
            queue.Enqueue(rootNode);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var delta = (cur.PeriodToDate - cur.PeriodFromDate).Days;
                if (delta <= 0) continue;

                var half = delta / 2;
                var leftEnd = cur.PeriodFromDate.AddDays(half);
                var rightStart = cur.PeriodFromDate.AddDays(half + 1);

                var leftNode = new BisectNode(GuidGenerator.Create(), entity.Id, cur.PeriodFromDate, leftEnd, cur.Id, CurrentTenant.Id);
                var rightNode = new BisectNode(GuidGenerator.Create(), entity.Id, rightStart, cur.PeriodToDate, cur.Id, CurrentTenant.Id);

                cur.LeftChildId = leftNode.Id;
                cur.RightChildId = rightNode.Id;

                await _nodeRepository.InsertAsync(leftNode, autoSave: true);
                await _nodeRepository.InsertAsync(rightNode, autoSave: true);
                await _nodeRepository.UpdateAsync(cur, autoSave: true);

                createdNodes.Add(leftNode);
                createdNodes.Add(rightNode);

                queue.Enqueue(leftNode);
                queue.Enqueue(rightNode);
            }
        }
        else
        {
            var stack = new Stack<BisectNode>();
            stack.Push(rootNode);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                var delta = (cur.PeriodToDate - cur.PeriodFromDate).Days;
                if (delta <= 0) continue;

                var half = delta / 2;
                var leftEnd = cur.PeriodFromDate.AddDays(half);
                var rightStart = cur.PeriodFromDate.AddDays(half + 1);

                var leftNode = new BisectNode(GuidGenerator.Create(), entity.Id, cur.PeriodFromDate, leftEnd, cur.Id, CurrentTenant.Id);
                var rightNode = new BisectNode(GuidGenerator.Create(), entity.Id, rightStart, cur.PeriodToDate, cur.Id, CurrentTenant.Id);

                cur.LeftChildId = leftNode.Id;
                cur.RightChildId = rightNode.Id;

                await _nodeRepository.InsertAsync(leftNode, autoSave: true);
                await _nodeRepository.InsertAsync(rightNode, autoSave: true);
                await _nodeRepository.UpdateAsync(cur, autoSave: true);

                createdNodes.Add(leftNode);
                createdNodes.Add(rightNode);

                stack.Push(rightNode);
                stack.Push(leftNode);
            }
        }

        entity.SetCurrentNode(rootNode.Id, rootNode.PeriodFromDate, rootNode.PeriodToDate, rootPl, rootBs);
        await _repository.UpdateAsync(entity, autoSave: true);

        return await GetAsync(entity.Id);
    }

    public async Task<BisectAccountingStatementsDto> BisectLeftAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (!entity.CurrentNodeId.HasValue)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:NoCurrentNode", "No current node selected.");
        }

        var currentNode = await _nodeRepository.GetAsync(entity.CurrentNodeId.Value);
        if (!currentNode.LeftChildId.HasValue)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:NoLeftChild", "No earlier (left) sub-period exists.");
        }

        var leftNode = await _nodeRepository.GetAsync(currentNode.LeftChildId.Value);
        if (!leftNode.IsGenerated)
        {
            var (pl, bs) = await CalculateSummaryAsync(entity.CompanyId, leftNode.PeriodFromDate, leftNode.PeriodToDate);
            leftNode.SetSummary(pl, bs);
            await _nodeRepository.UpdateAsync(leftNode, autoSave: true);
        }

        entity.SetCurrentNode(leftNode.Id, leftNode.PeriodFromDate, leftNode.PeriodToDate, leftNode.PlSummary, leftNode.BsSummary);
        await _repository.UpdateAsync(entity, autoSave: true);

        return await GetAsync(entity.Id);
    }

    public async Task<BisectAccountingStatementsDto> BisectRightAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (!entity.CurrentNodeId.HasValue)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:NoCurrentNode", "No current node selected.");
        }

        var currentNode = await _nodeRepository.GetAsync(entity.CurrentNodeId.Value);
        if (!currentNode.RightChildId.HasValue)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:NoRightChild", "No later (right) sub-period exists.");
        }

        var rightNode = await _nodeRepository.GetAsync(currentNode.RightChildId.Value);
        if (!rightNode.IsGenerated)
        {
            var (pl, bs) = await CalculateSummaryAsync(entity.CompanyId, rightNode.PeriodFromDate, rightNode.PeriodToDate);
            rightNode.SetSummary(pl, bs);
            await _nodeRepository.UpdateAsync(rightNode, autoSave: true);
        }

        entity.SetCurrentNode(rightNode.Id, rightNode.PeriodFromDate, rightNode.PeriodToDate, rightNode.PlSummary, rightNode.BsSummary);
        await _repository.UpdateAsync(entity, autoSave: true);

        return await GetAsync(entity.Id);
    }

    public async Task<BisectAccountingStatementsDto> MoveUpAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (!entity.CurrentNodeId.HasValue)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:NoCurrentNode", "No current node selected.");
        }

        var currentNode = await _nodeRepository.GetAsync(entity.CurrentNodeId.Value);
        if (!currentNode.ParentNodeId.HasValue)
        {
            throw new BusinessException("MyERP:BisectAccountingStatements:ReachedRoot", "Already at root period.");
        }

        var parentNode = await _nodeRepository.GetAsync(currentNode.ParentNodeId.Value);
        if (!parentNode.IsGenerated)
        {
            var (pl, bs) = await CalculateSummaryAsync(entity.CompanyId, parentNode.PeriodFromDate, parentNode.PeriodToDate);
            parentNode.SetSummary(pl, bs);
            await _nodeRepository.UpdateAsync(parentNode, autoSave: true);
        }

        entity.SetCurrentNode(parentNode.Id, parentNode.PeriodFromDate, parentNode.PeriodToDate, parentNode.PlSummary, parentNode.BsSummary);
        await _repository.UpdateAsync(entity, autoSave: true);

        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.BisectAccountingStatements.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var nodesQueryable = await _nodeRepository.GetQueryableAsync();
        var nodes = await AsyncExecuter.ToListAsync(nodesQueryable.Where(n => n.BisectAccountingStatementsId == id));

        foreach (var node in nodes)
        {
            await _nodeRepository.DeleteAsync(node, autoSave: true);
        }

        await _repository.DeleteAsync(entity, autoSave: true);
    }

    private async Task<(decimal plSummary, decimal bsSummary)> CalculateSummaryAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var journalQueryable = await _journalRepository.GetQueryableAsync();
        var journals = await AsyncExecuter.ToListAsync(journalQueryable
            .Where(j => j.CompanyId == companyId
                     && j.Status == DocumentStatus.Posted
                     && j.PostingDate >= fromDate.Date
                     && j.PostingDate <= toDate.Date));

        var accounts = (await _accountRepository.GetListAsync()).ToDictionary(a => a.Id, a => a.AccountType);

        decimal totalRevenue = 0;
        decimal totalExpense = 0;
        decimal netAssets = 0;
        decimal netLiabilities = 0;
        decimal netEquity = 0;

        foreach (var j in journals)
        {
            foreach (var line in j.Lines)
            {
                if (!accounts.TryGetValue(line.AccountId, out var accType)) continue;

                switch (accType)
                {
                    case AccountType.Revenue:
                        totalRevenue += line.IsDebit ? -line.Amount : line.Amount;
                        break;
                    case AccountType.Expense:
                        totalExpense += line.IsDebit ? line.Amount : -line.Amount;
                        break;
                    case AccountType.Asset:
                        netAssets += line.IsDebit ? line.Amount : -line.Amount;
                        break;
                    case AccountType.Liability:
                        netLiabilities += line.IsDebit ? -line.Amount : line.Amount;
                        break;
                    case AccountType.Equity:
                        netEquity += line.IsDebit ? -line.Amount : line.Amount;
                        break;
                }
            }
        }

        var pl = totalRevenue - totalExpense;
        var bs = netAssets - netLiabilities - netEquity;

        return (pl, bs);
    }
}
