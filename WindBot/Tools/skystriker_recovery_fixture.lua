-- Test-only board constructor. Never installed in MDPro4 or shipped to a duel.
-- The runner substitutes CASE_ID and adds this separate card to an isolated CDB.
-- Existing card scripts remain byte-identical. Only initial state and fixture
-- termination are artificial; responses, costs, targets and resolution are OCG.
local s,id=GetID()
local case_id=CASE_ID
function s.initial_effect(c)
    local tp=0 -- runner fixes the tested bot as first player
    local e=Effect.CreateEffect(c)
    e:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
    e:SetCode(EVENT_PHASE+PHASE_STANDBY)
    e:SetCountLimit(1)
    e:SetOperation(function(e)
        if Duel.GetTurnCount()~=(case_id==5 and 3 or 1) then return end
        e:Reset()
        Duel.Remove(Duel.GetFieldGroup(tp,LOCATION_HAND,LOCATION_HAND),POS_FACEDOWN,REASON_RULE)
        local function card(code,p,loc,seq)
            local g=Duel.GetMatchingGroup(Card.IsCode,p,LOCATION_DECK+LOCATION_EXTRA,0,nil,code)
            local tc=g:GetFirst()
            if not tc then tc=Duel.CreateToken(p,code) end
            if loc==LOCATION_MZONE or loc==LOCATION_SZONE then
                Duel.MoveToField(tc,p,p,loc,loc==LOCATION_MZONE and POS_FACEUP_ATTACK or POS_FACEDOWN,true,1<<seq)
                if loc==LOCATION_MZONE then tc:CompleteProcedure() end
            elseif loc==LOCATION_GRAVE then Duel.SendtoGrave(tc,REASON_RULE)
            else Duel.SendtoHand(tc,p,REASON_RULE) end
            return tc
        end
        if case_id==1 then
            card(51227866,tp,LOCATION_SZONE,0)
            card(98338152,tp,LOCATION_SZONE,1)
            card(98829635,tp,LOCATION_SZONE,2)
            card(55623480,1-tp,LOCATION_GRAVE,0)
            card(18144506,1-tp,LOCATION_HAND,0)
        elseif case_id==2 or case_id==3 or case_id==6 then
            card(38817295,tp,LOCATION_SZONE,0)
            card(26077387,tp,LOCATION_MZONE,0)
            card(80280944,1-tp,LOCATION_MZONE,1)
            card(case_id==3 and 4779823 or 95503687,1-tp,LOCATION_MZONE,0)
            if case_id==2 then
                card(77558536,1-tp,LOCATION_GRAVE,0)
                card(89631139,1-tp,LOCATION_HAND,0)
            end
        elseif case_id==4 then
            card(26077387,tp,LOCATION_MZONE,0)
            card(37351133,tp,LOCATION_MZONE,1)
            card(70368879,tp,LOCATION_HAND,0)
            Duel.SetLP(1-tp,2500)
        elseif case_id==5 then
            card(89631139,tp,LOCATION_MZONE,0)
            card(41462083,1-tp,LOCATION_MZONE,0)
            card(34433770,tp,LOCATION_HAND,0)
            for _,code in ipairs({98338152,51227866,76072561,90673288}) do
                card(code,tp,LOCATION_GRAVE,0)
            end
            Duel.SetLP(1-tp,600)
        end
        -- Let the set Quick-Play cards age for a turn. No strategic decision by
        -- the tested bot is simulated; it receives actual turn-two chain offers.
        local skips=case_id==5 and {} or {EFFECT_SKIP_M1,EFFECT_SKIP_BP,EFFECT_SKIP_M2}
        for _,code in ipairs(skips) do
            local skip=Effect.CreateEffect(c)
            skip:SetType(EFFECT_TYPE_FIELD)
            skip:SetProperty(EFFECT_FLAG_PLAYER_TARGET)
            skip:SetCode(code)
            skip:SetTargetRange(1,0)
            skip:SetReset(RESET_PHASE+PHASE_END)
            Duel.RegisterEffect(skip,tp)
        end
    end)
    Duel.RegisterEffect(e,tp)
    local finish=Effect.CreateEffect(c)
    finish:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
    finish:SetCode(EVENT_PHASE+PHASE_END)
    finish:SetCountLimit(1)
    finish:SetOperation(function()
        local final_turn=(case_id==4 or case_id==5) and 3 or 2
        if Duel.GetTurnCount()>=final_turn then Duel.Win(tp,0x55) end
    end)
    Duel.RegisterEffect(finish,tp)
end
